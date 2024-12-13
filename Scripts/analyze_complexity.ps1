# Code Complexity Analysis Script
# Analyzes code complexity metrics including cyclomatic complexity, nesting depth, and code smells

param (
    [Parameter(Mandatory=$false)]
    [string]$ProjectDir = $PWD,
    
    [Parameter(Mandatory=$false)]
    [int]$ComplexityThreshold = 10,
    
    [Parameter(Mandatory=$false)]
    [int]$NestingThreshold = 4,
    
    [Parameter(Mandatory=$false)]
    [switch]$ExportCsv,
    
    [Parameter(Mandatory=$false)]
    [switch]$Detailed
)

function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Get-NestingDepth {
    param ([string]$line)
    $depth = 0
    $maxDepth = 0
    foreach ($char in $line.ToCharArray()) {
        if ($char -eq '{') { $depth++ }
        if ($char -eq '}') { $depth-- }
        if ($depth -gt $maxDepth) { $maxDepth = $depth }
    }
    return $maxDepth
}

function Get-CodeMetrics {
    param ([string]$filePath)
    
    $content = Get-Content $filePath
    $metrics = @{
        Methods = 0
        Loops = 0
        Conditionals = 0
        MaxNestingDepth = 0
        LongMethods = 0
        ComplexMethods = 0
        CodeSmells = @()
        LineCount = $content.Count
        CommentCount = 0
        TODOCount = 0
    }
    
    $currentMethodLines = 0
    $currentMethodComplexity = 0
    $inMethod = $false
    $nestingDepth = 0
    $maxNestingDepth = 0
    
    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        
        # Track comments and TODOs
        if ($line -match "^\s*//") {
            $metrics.CommentCount++
            if ($line -match "TODO") { $metrics.TODOCount++ }
        }
        
        # Track method boundaries
        if ($line -match "^\s*(public|private|protected|internal).*\s+\w+\s*\(.*\)\s*({|\s*$)") {
            $metrics.Methods++
            $inMethod = $true
            $currentMethodLines = 0
            $currentMethodComplexity = 1
            $nestingDepth = 1
            $maxNestingDepth = 1
        }
        
        if ($inMethod) {
            $currentMethodLines++
            
            # Track nesting depth
            $nestingDepth += ([regex]::Matches($line, "{")).Count
            $nestingDepth -= ([regex]::Matches($line, "}")).Count
            if ($nestingDepth -gt $maxNestingDepth) {
                $maxNestingDepth = $nestingDepth
            }
            
            # Track complexity
            if ($line -match "\s*(if|while|for|foreach|switch)\s*\(") {
                $currentMethodComplexity++
                $metrics.Conditionals++
            }
            if ($line -match "\s*(for|foreach|while|do)\s*\(") {
                $metrics.Loops++
            }
            
            # Check for method end
            if ($line -match "^\s*}") {
                if ($currentMethodLines -gt 30) {
                    $metrics.LongMethods++
                    $metrics.CodeSmells += "Long method (${currentMethodLines} lines)"
                }
                if ($currentMethodComplexity -gt $ComplexityThreshold) {
                    $metrics.ComplexMethods++
                    $metrics.CodeSmells += "Complex method (complexity: ${currentMethodComplexity})"
                }
                if ($maxNestingDepth -gt $NestingThreshold) {
                    $metrics.CodeSmells += "Deep nesting (depth: ${maxNestingDepth})"
                }
                $inMethod = $false
                $metrics.MaxNestingDepth = [Math]::Max($metrics.MaxNestingDepth, $maxNestingDepth)
            }
        }
    }
    
    return $metrics
}

Write-ColorOutput Cyan "Starting Code Complexity Analysis..."
Write-Output "Project Directory: $ProjectDir"
Write-Output ("-" * 80)

$results = @()

Get-ChildItem -Path $ProjectDir -Recurse -Filter "*.cs" | 
    Where-Object { $_.FullName -notmatch "\\(bin|obj|packages)\\" } |
    ForEach-Object {
        Write-ColorOutput Yellow "Analyzing $($_.Name)..."
        $metrics = Get-CodeMetrics $_.FullName
        
        $result = [PSCustomObject]@{
            File = $_.Name
            Methods = $metrics.Methods
            Loops = $metrics.Loops
            Conditionals = $metrics.Conditionals
            MaxNestingDepth = $metrics.MaxNestingDepth
            LongMethods = $metrics.LongMethods
            ComplexMethods = $metrics.ComplexMethods
            LineCount = $metrics.LineCount
            CommentCount = $metrics.CommentCount
            TODOCount = $metrics.TODOCount
            TotalComplexity = $metrics.Methods + $metrics.Loops + $metrics.Conditionals
            CodeSmells = $metrics.CodeSmells -join "`n"
        }
        
        $results += $result
        
        if ($Detailed) {
            Write-ColorOutput Green "`nDetailed Analysis for $($_.Name):"
            Write-Output "Methods: $($metrics.Methods)"
            Write-Output "Complex Methods: $($metrics.ComplexMethods)"
            Write-Output "Maximum Nesting Depth: $($metrics.MaxNestingDepth)"
            Write-Output "Line Count: $($metrics.LineCount)"
            Write-Output "Comment Count: $($metrics.CommentCount)"
            Write-Output "TODO Count: $($metrics.TODOCount)"
            
            if ($metrics.CodeSmells.Count -gt 0) {
                Write-ColorOutput Red "`nCode Smells:"
                $metrics.CodeSmells | ForEach-Object { Write-Output "- $_" }
            }
            Write-Output ("-" * 40)
        }
    }

# Summary
Write-ColorOutput Cyan "`nAnalysis Summary:"
Write-Output ("-" * 80)
$totalFiles = $results.Count
$totalComplexity = ($results | Measure-Object -Property TotalComplexity -Sum).Sum
$avgComplexity = [math]::Round($totalComplexity / $totalFiles, 2)
$complexFiles = ($results | Where-Object { $_.ComplexMethods -gt 0 }).Count

Write-Output "Total Files Analyzed: $totalFiles"
Write-Output "Average Complexity: $avgComplexity"
Write-Output "Files with Complex Methods: $complexFiles"

# Export results if requested
if ($ExportCsv) {
    $exportPath = Join-Path $ProjectDir "complexity_analysis.csv"
    $results | Export-Csv -Path $exportPath -NoTypeInformation
    Write-ColorOutput Green "Results exported to: $exportPath"
}

# Display most complex files
Write-ColorOutput Yellow "`nMost Complex Files:"
Write-Output ("-" * 80)
$results | Sort-Object TotalComplexity -Descending | Select-Object -First 5 | 
    Format-Table File, TotalComplexity, ComplexMethods, MaxNestingDepth -AutoSize
