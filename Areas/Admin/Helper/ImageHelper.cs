using System;
using System.IO;
using System.Web;



namespace CinemaWeb.Areas.Admin.Helper
{
    public class ImageHelper
    {


        public static string CreateCid()
        {
            return $"cid:{Guid.NewGuid()}";
        }
    }
}
