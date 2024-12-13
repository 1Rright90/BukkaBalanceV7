using namespace System
using namespace System.Collections.Generic
using namespace System.Management.Automation
using namespace System.Collections.Concurrent
using namespace System.Threading
using namespace System.Text

# Initialize basic logging with timestamp and color support
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "Info"
    )
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $color = switch ($Level) {
        "Error" { "Red" }
        "Warning" { "Yellow" }
        "Success" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] $Message" -ForegroundColor $color
}

# Initialize metrics using a thread-safe ConcurrentDictionary
$script:Metrics = [ConcurrentDictionary[string,object]]::new()
$null = $script:Metrics.TryAdd("TotalFiles", 0)
$null = $script:Metrics.TryAdd("FilesWithThreading", 0)
$null = $script:Metrics.TryAdd("TotalComplexity", 0)
$null = $script:Metrics.TryAdd("Issues", [System.Collections.Concurrent.ConcurrentBag[object]]::new())

# Configuration using immutable collections for thread safety
$script:config = @{
    ConcurrencyPatterns = @{
        ConcurrentCollections = @(
            '(?<!//.*)\bConcurrentDictionary<',
            '(?<!//.*)\bConcurrentQueue<',
            '(?<!//.*)\bConcurrentBag<',
            '(?<!//.*)\bBlockingCollection<',
            '(?<!//.*)\bImmutableArray<',
            '(?<!//.*)\bImmutableList<',
            '(?<!//.*)\bImmutableDictionary<',
            '(?<!//.*)\bChannelReader<',
            '(?<!//.*)\bChannelWriter<',
            '(?<!//.*)\bIProducerConsumerCollection<'
        ).AsReadOnly()
        SynchronizationPrimitives = @(
            '(?<!//.*)\bInterlocked\.',
            '(?<!//.*)\bMonitor\.',
            '(?<!//.*)\block\s*\(',
            '(?<!//.*)\bSpinLock\b',
            '(?<!//.*)\bSemaphoreSlim\b',
            '(?<!//.*)\bReaderWriterLockSlim\b',
            '(?<!//.*)\bMutex\b',
            '(?<!//.*)\bManualResetEventSlim\b',
            '(?<!//.*)\bAutoResetEvent\b'
        ).AsReadOnly()
        AsyncPatterns = @(
            '(?<!//.*)\basync\s+\w+.*\(',
            '(?<!//.*)\bawait\s+',
            '(?<!//.*)\bTask\.Run\(',
            '(?<!//.*)\bParallel\.',
            '(?<!//.*)\bTaskScheduler\.',
            '(?<!//.*)\bValueTask<',
            '(?<!//.*)\bIAsyncEnumerable<'
        ).AsReadOnly()
        DataflowPatterns = @(
            '(?<!//.*)\bActionBlock<',
            '(?<!//.*)\bTransformBlock<',
            '(?<!//.*)\bBatchBlock<',
            '(?<!//.*)\bBufferBlock<',
            '(?<!//.*)\bBroadcastBlock<'
        ).AsReadOnly()
    }
    ThreadingIssues = @{
        BlockingCalls = @(
            '(?<!//.*)\bThread\.Sleep\(',
            '(?<!//.*?)(?<!Configure)\bWait\s*\(',
            '(?<!//.*)\bTask\.Result\b',
            '(?<!//.*)\bGetAwaiter\(\)\.GetResult\(\)',
            '(?<!//.*)\bWaitOne\s*\(',
            '(?<!//.*)\bWaitAll\s*\(',
            '(?<!//.*)\bWaitAny\s*\('
        ).AsReadOnly()
        ThreadPoolIssues = @(
            '(?<!//.*)\bThreadPool\.QueueUserWorkItem\(',
            '(?<!//.*)\bThread\s*=\s*new\s+Thread\(',
            '(?<!//.*)\bBackgroundWorker\b'
        ).AsReadOnly()
        AsyncVoidMethods = '(?<!//.*)\basync\s+void\s+\w+'
        MissingConfigureAwait = '(?<!//.*)\bawait\s+(?!.*ConfigureAwait)'
    }
    HighRiskPatterns = @{
        Singleton = '(?<!//.*)\bprivate\s+static\s+\w+\s+\w+\s*;\s*private\s+static\s+readonly\s+object\s+\w+\s*='
        DoubleCheckedLocking = '(?<!//.*)\bif\s*\(\w+\s*==\s*null\)\s*{\s*lock\s*\('
        StaticMutableState = '(?<!//.*)\bprivate\s+static\s+(?!readonly)\w+\s+\w+'
        UnsynchronizedCollections = '(?<!//.*)\b(List<|HashSet<|Dictionary<|Stack<|Queue<)(?!.*private\s+readonly)'
        RaceConditions = '(?<!//.*)\bvolatile\s+(?!bool)'
    }
    RequiredSynchronization = @(
        'FormationManager',
        'SpawnQueueManager',
        'ResourceManager',
        'NetworkMessageProcessor',
        'PerformanceMonitor',
        'MemoryProfiler'
    ).AsReadOnly()
}

# Resolutions with immutable examples
$script:resolutions = @{
    "BlockingCall" = @{
        Severity = "Critical"
        Resolution = "Consider using asynchronous alternatives (e.g., Task.Delay instead of Thread.Sleep) and avoid blocking threads."
        FixExample = @{
            "Thread.Sleep" = "await Task.Delay().ConfigureAwait(false)"
            "Task.Result" = "await task.ConfigureAwait(false)"
            "WaitOne" = "await WaitOneAsync().ConfigureAwait(false)"
        }.AsReadOnly()
    }
    "ThreadPoolIssue" = @{
        Severity = "High"
        Resolution = "Avoid direct ThreadPool usage. Prefer higher-level concurrency APIs."
        FixExample = @{
            "ThreadPool.QueueUserWorkItem" = "await Task.Run(() => { }).ConfigureAwait(false)"
            "new Thread" = "await Task.Run(() => { }).ConfigureAwait(false)"
        }.AsReadOnly()
    }
    "AsyncVoid" = @{
        Severity = "High"
        Resolution = "Use async Task instead of async void, except for event handlers."
        FixExample = @{
            "async void Method" = "async Task Method"
        }.AsReadOnly()
    }
    "MissingConfigureAwait" = @{
        Severity = "Medium"
        Resolution = "Add ConfigureAwait(false) to await calls in library code to prevent context capturing."
        FixExample = @{
            "await task" = "await task.ConfigureAwait(false)"
        }.AsReadOnly()
    }
    "HighRisk" = @{
        Severity = "Critical"
        Resolution = "This pattern can cause threading issues. Consider safer concurrency patterns or proper synchronization."
        FixExample = @{
            "Dictionary<" = "ConcurrentDictionary<"
            "List<" = "ConcurrentBag< or ImmutableList<"
            "private static" = "private static readonly"
            "volatile" = "Interlocked operations"
        }.AsReadOnly()
    }
}.AsReadOnly()

# Compile patterns once and store in thread-safe collections
$script:CompiledPatterns = @{
    ConcurrentCollections = [ConcurrentDictionary[string, Regex]]::new()
    SynchronizationPrimitives = [ConcurrentDictionary[string, Regex]]::new()
    AsyncPatterns = [ConcurrentDictionary[string, Regex]]::new()
    DataflowPatterns = [ConcurrentDictionary[string, Regex]]::new()
    ThreadingIssues = [ConcurrentDictionary[string, Regex]]::new()
    HighRiskPatterns = [ConcurrentDictionary[string, Regex]]::new()
}

# Initialize patterns with compiled regex
function Initialize-CompiledPatterns {
    foreach ($pattern in $script:config.ConcurrencyPatterns.ConcurrentCollections) {
        $null = $script:CompiledPatterns.ConcurrentCollections.TryAdd(
            $pattern, 
            [Regex]::new($pattern, [RegexOptions]::Compiled -bor [RegexOptions]::IgnoreCase)
        )
    }
    foreach ($pattern in $script:config.ConcurrencyPatterns.SynchronizationPrimitives) {
        $null = $script:CompiledPatterns.SynchronizationPrimitives.TryAdd(
            $pattern,
            [Regex]::new($pattern, [RegexOptions]::Compiled -bor [RegexOptions]::IgnoreCase)
        )
    }
    # Add other pattern initializations...
}

# Optimized line offset calculation using binary search
function Get-LineNumber {
    param(
        [int[]]$LineOffsets,
        [int]$Position
    )
    
    $low = 0
    $high = $LineOffsets.Length - 1
    
    while ($low -le $high) {
        $mid = $low + [Math]::Floor(($high - $low) / 2)
        if ($LineOffsets[$mid] -le $Position -and 
            ($mid -eq $LineOffsets.Length - 1 -or $LineOffsets[$mid + 1] -gt $Position)) {
            return $mid + 1
        }
        if ($LineOffsets[$mid] -gt $Position) {
            $high = $mid - 1
        }
        else {
            $low = $mid + 1
        }
    }
    return 1
}

# Optimized file content processing using memory-efficient streaming
function Get-FileContent {
    param(
        [string]$FilePath,
        [int]$ChunkSize = 4MB
    )
    
    try {
        $stream = [System.IO.File]::OpenRead($FilePath)
        $reader = [System.IO.StreamReader]::new($stream)
        $content = [StringBuilder]::new()
        
        $buffer = New-Object char[] $ChunkSize
        $bytesRead = 0
        
        while (($bytesRead = $reader.Read($buffer, 0, $buffer.Length)) -gt 0) {
            [void]$content.Append($buffer, 0, $bytesRead)
        }
        
        return $content.ToString()
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($stream) { $stream.Dispose() }
    }
}

# Main analysis function with parallel processing
function Get-ThreadingIssues {
    param(
        [string]$ProjectDir,
        [int]$MaxParallelism = 4
    )
    
    Write-Log "Starting threading analysis for $ProjectDir" -Level "Info"
    
    if (-not (Test-Path $ProjectDir)) {
        Write-Log "Project directory not found: $ProjectDir" -Level "Error"
        return
    }
    
    # Initialize compiled patterns
    Initialize-CompiledPatterns
    
    # Get all C# files excluding tests
    $files = Get-ChildItem -Path $ProjectDir -Filter *.cs -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\Tests\\' }
    
    if (-not $files) {
        Write-Log "No C# files found in $ProjectDir" -Level "Warning"
        return
    }
    
    Write-Log "Found $($files.Count) files to analyze" -Level "Info"
    
    # Create thread-safe collection for results
    $results = [ConcurrentBag[object]]::new()
    
    # Process files in parallel with throttling
    $files | ForEach-Object -ThrottleLimit $MaxParallelism -Parallel {
        $file = $_
        
        try {
            # Process file content in chunks
            $content = Get-FileContent -FilePath $file.FullName
            if ([string]::IsNullOrWhiteSpace($content)) { return }
            
            # Analyze file
            $analysis = @{
                File = $file.FullName
                Issues = [ConcurrentBag[object]]::new()
                Metrics = @{
                    ThreadingComplexity = 0
                    AsyncUsage = 0
                    ConcurrentCollections = 0
                    SynchronizationPrimitives = 0
                }
            }
            
            # Analyze patterns
            foreach ($pattern in $using:CompiledPatterns.Values) {
                $patternMatches = $pattern.Matches($content)  
                foreach ($foundMatch in $patternMatches) {  
                    $lineNumber = Get-LineNumber -LineOffsets $lineOffsets -Position $foundMatch.Index
                    $analysis.Issues.Add(@{
                        Line = $lineNumber
                        Pattern = $pattern.ToString()
                        Match = $foundMatch.Value
                    })
                }
            }
            
            $results.Add($analysis)
        }
        catch {
            Write-Log "Error processing $($file.Name): $_" -Level "Error"
        }
    }
    
    # Aggregate results
    $summary = @{
        TotalFiles = $files.Count
        FilesWithIssues = ($results | Where-Object { $_.Issues.Count -gt 0 }).Count
        TotalIssues = ($results | ForEach-Object { $_.Issues.Count } | Measure-Object -Sum).Sum
        Issues = $results | Where-Object { $_.Issues.Count -gt 0 } | ForEach-Object {
            @{
                File = $_.File
                Issues = $_.Issues
                Metrics = $_.Metrics
            }
        }
    }
    
    # Output summary
    Write-Log "`nAnalysis Summary:" -Level "Success"
    Write-Log "Total Files: $($summary.TotalFiles)" -Level "Info"
    Write-Log "Files with Issues: $($summary.FilesWithIssues)" -Level "Info"
    Write-Log "Total Issues: $($summary.TotalIssues)" -Level "Info"
    
    if ($summary.Issues) {
        Write-Log "`nDetailed Issues:" -Level "Warning"
        foreach ($fileIssue in $summary.Issues) {
            Write-Log "`nFile: $($fileIssue.File)" -Level "Info"
            foreach ($issue in $fileIssue.Issues) {
                Write-Log "  Line $($issue.Line): $($issue.Pattern) - $($issue.Match)" -Level "Warning"
            }
        }
    }
    
    return $summary
}

# Export the main function
Export-ModuleMember -Function Get-ThreadingIssues