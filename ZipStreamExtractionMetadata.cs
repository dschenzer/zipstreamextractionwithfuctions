using System.Collections.Generic;

namespace schenzer.zipstream
{
    internal class ZipStreamExtractionMetadata
    {
        public string ArchiveFileName { get; internal set; }
        public long ArchiveFileLength { get; internal set; }
        public List<ZipStreamArchiveEntry> FileToBeExtracted { get; internal set; }
    }
}