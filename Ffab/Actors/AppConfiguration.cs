using System;

namespace Ffab.Actors
{
    /// <summary>
    /// Configuration for the application.
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Base directory for downloads to go into.
        /// </summary>
        public string DownloadBaseDir { get; set; } = "Input";
        
        /// <summary>
        /// Base directory for video output to go into.
        /// </summary>
        public string OutputBaseDir { get; set; } = "Output";

        /// <summary>
        /// Name of the upload bucket.
        /// </summary>
        public string UploadBucketName { get; set; } = "coachyard-video";

        /// <summary>
        /// Size of in-memory download buffer.
        /// </summary>
        public int DownloadBufferSize { get; set; } = 1024;
        
        /// <summary>
        /// How many downloaders to spin up.
        /// </summary>
        public int NumDownloaders { get; set; } = 12;
        
        /// <summary>
        /// How many uploaders to spin up.
        /// </summary>
        public int NumUploaders { get; set; } = 12;
        
        /// <summary>
        /// Number of processors.
        /// </summary>
        public int NumProcessors { get; set; } = Environment.ProcessorCount;
    }
}