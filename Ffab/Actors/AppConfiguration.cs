using System;

namespace Ffab.Actors
{
    public class AppConfiguration
    {
        public string DownloadBaseDir { get; set; } = "Input";
        public string OutputBaseDir { get; set; } = "Output";

        public string UploadBucketName { get; set; } = "coachyard-video";

        public int DownloadBufferSize { get; set; } = 1024;
            
        public int NumDownloaders { get; set; } = 12;
        public int NumUploaders { get; set; } = 12;
        public int NumProcessors { get; set; } = Environment.ProcessorCount;
    }
}