using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace ScannerSample.Wpf.Imaging
{
    public static class TiffDocumentWriter
    {
        public static string SaveMultiPageTiff(IEnumerable<string> pageFiles, string outputFile)
        {
            var files = pageFiles.ToList();
            if (files.Count == 0)
            {
                return null;
            }

            var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(x => x.MimeType == "image/tiff");
            if (encoder == null)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));

            using (var first = Image.FromFile(files[0]))
            {
                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                first.Save(outputFile, encoder, encoderParameters);

                encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
                for (var i = 1; i < files.Count; i++)
                {
                    using (var page = Image.FromFile(files[i]))
                    {
                        first.SaveAdd(page, encoderParameters);
                    }
                }

                encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
                first.SaveAdd(encoderParameters);
            }

            return outputFile;
        }
    }
}
