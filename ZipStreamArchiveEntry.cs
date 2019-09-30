namespace schenzer.zipstream
{
    internal class ZipStreamArchiveEntry
    {
        public string DestinationBlobName { get; internal set; }
        public string OriginalFileName { get; internal set; }
        public string ShortFileName { get; internal set; }
    }
}