using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFToolsLite
{
    public class FileItem
    {
        /// <summary>
        /// Display name of the file (e.g. "document.pdf").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Full absolute path of the file on disk.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Page count of the PDF.
        /// </summary>
        public int PageCount { get; set; }
    }
}
