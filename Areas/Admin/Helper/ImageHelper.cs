using System;
using System.IO;
using System.Web;



namespace CinemaWeb.Areas.Admin.Helper
{
    public class ImageHelper
    {
        public static string ConvertFileToBase64(HttpPostedFileBase file)
        {
            try
            {
                byte[] imageBytes;
                using (BinaryReader reader = new BinaryReader(file.InputStream))
                {
                    imageBytes = reader.ReadBytes(file.ContentLength);
                }
                return Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Error converting file to base64: " + ex.Message);
            }
        }

        public static string CreateCid()
        {
            return $"cid:{Guid.NewGuid()}";
        }
    }
}
