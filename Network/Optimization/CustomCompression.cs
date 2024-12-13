using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using YSBCaptain.Core.Error;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Network.Optimization
{
    /// <summary>
    /// Handles custom compression for network packets with performance monitoring
    /// </summary>
    public class CustomCompression
    {
        private readonly IErrorHandler _errorHandler;
        private readonly IPerformanceMonitor _performanceMonitor;
        private const int CompressionThreshold = 1024; // Only compress data larger than 1KB
        private const int MaxUncompressedSize = 10 * 1024 * 1024; // 10MB max uncompressed size
        private const double MinCompressionRatio = 0.8; // Only keep compressed if it saves at least 20%

        public CustomCompression(IErrorHandler errorHandler, IPerformanceMonitor performanceMonitor)
        {
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Compresses data if it meets size and efficiency thresholds
        /// </summary>
        public async Task<byte[]> CompressAsync(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                _errorHandler.HandleWarning("Compression_EmptyData", "Attempted to compress null or empty data");
                return data;
            }

            if (data.Length > MaxUncompressedSize)
            {
                _errorHandler.HandleWarning("Compression_DataTooLarge", 
                    $"Data size ({data.Length} bytes) exceeds maximum uncompressed size");
                return data;
            }

            if (data.Length < CompressionThreshold)
            {
                _performanceMonitor.TrackMetric("Compression_SkippedSmallData", 1);
                return data;
            }

            try
            {
                var startTime = DateTime.UtcNow;
                using (var compressedStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
                    {
                        await gzipStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    }

                    var compressedData = compressedStream.ToArray();
                    var compressionRatio = (double)compressedData.Length / data.Length;
                    var compressionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                    _performanceMonitor.TrackMetric("Compression_Time", compressionTime);
                    _performanceMonitor.TrackMetric("Compression_Ratio", compressionRatio);

                    if (compressionRatio <= MinCompressionRatio)
                    {
                        return compressedData;
                    }
                    else
                    {
                        _performanceMonitor.TrackMetric("Compression_IneffectiveCompression", 1);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError("Compression_Failed", ex);
                return data;
            }
        }

        /// <summary>
        /// Decompresses data that was previously compressed
        /// </summary>
        public async Task<byte[]> DecompressAsync(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
            {
                _errorHandler.HandleWarning("Decompression_EmptyData", "Attempted to decompress null or empty data");
                return compressedData;
            }

            try
            {
                var startTime = DateTime.UtcNow;
                using (var compressedStream = new MemoryStream(compressedData))
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    await gzipStream.CopyToAsync(resultStream).ConfigureAwait(false);
                    var decompressedData = resultStream.ToArray();

                    if (decompressedData.Length > MaxUncompressedSize)
                    {
                        throw new InvalidOperationException($"Decompressed size ({decompressedData.Length} bytes) exceeds maximum");
                    }

                    var decompressionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    _performanceMonitor.TrackMetric("Decompression_Time", decompressionTime);
                    _performanceMonitor.TrackMetric("Decompression_SizeRatio", 
                        (double)decompressedData.Length / compressedData.Length);

                    return decompressedData;
                }
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError("Decompression_Failed", ex);
                return compressedData;
            }
        }

        /// <summary>
        /// Checks if data should be compressed based on size threshold
        /// </summary>
        public bool ShouldCompress(int dataSize)
        {
            return dataSize >= CompressionThreshold && dataSize <= MaxUncompressedSize;
        }

        /// <summary>
        /// Estimates compressed size based on typical compression ratios
        /// </summary>
        public int EstimateCompressedSize(int originalSize)
        {
            if (originalSize < CompressionThreshold)
                return originalSize;

            // Use conservative estimate of 70% of original size
            return (int)(originalSize * 0.7);
        }
    }
}
