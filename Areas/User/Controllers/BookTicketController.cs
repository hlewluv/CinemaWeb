using CinemaWeb.App_Start;
using CinemaWeb.Models;
using CinemaWeb.SupportFile;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Mail;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Web;
using System.Web.DynamicData;
using System.Web.Mvc;
using System.Web.Services.Description;
using WebGrease.Css.Extensions;

namespace CinemaWeb.Areas.User.Controllers
{
    public class BookTicketController : Controller
    {
        Cinema_Web_Entities db = new Cinema_Web_Entities();
        IHubContext _hubContext = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
        public enum RoleName
        {
            AddMovie = 1, 
            RemoveMovie = 2,
            EditMovie = 3,
            Statistical = 4,
            AddRoom = 5,
            RemoveRoom = 6,
            AddSchedule = 7,
            RemoveSchedule = 8,
            AddStaff = 9,
            RemoveStaff = 10,
            RemoveUser = 11,
            BookTicket = 12,
            EditProfile = 13,
            Payment = 14,
            CheckIn = 15
        }
        public void GetMovieStatus(List<movy> movielist)
        {
            DateTime currentDate = DateTime.Now;
            foreach (movy movie in movielist)
            {
                if (movie.release_date <= currentDate && movie.end_date >= currentDate)
                {
                    movie.movie_status = true; // Đang chiếu
                }
                else if (movie.release_date > currentDate)
                {
                    movie.movie_status = false; // Sắp chiếu
                }
                else
                {
                    movie.movie_status = null;
                }
            }
        }
        // GET: User/BookTicket

        //[UserAuthorize(roleId = (int)RoleName.BookTicket)]
        public ActionResult BookTicket()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
            return View();
        }
        [HttpGet]
        public ActionResult GetDisplayDate(int movieId)
        {
            var movieDateList = db.movie_display_date
                        .Where(x => x.movie_id == movieId)
                        .Select(x => new {
                            x.display_date.id,
                            x.display_date.display_date1
                        })
                        .ToList();
           
            return Json(movieDateList, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult GetScheduleTime(int displaydateId, int movieId)
        {
            var scheduleList = db.schedule_detail
                        .Where(x => x.movie_display_date.display_date_id == displaydateId &&
                                    x.movie_display_date.movie_id == movieId)
                        .Select(x => new
                        {
                            x.schedule.id,
                            schedule_time = DbFunctions.CreateDateTime(x.movie_display_date.display_date.display_date1.Value.Year,
                                                                       x.movie_display_date.display_date.display_date1.Value.Month,
                                                                       x.movie_display_date.display_date.display_date1.Value.Day, 
                                                                       x.schedule.schedule_time.Value.Hours,
                                                                       x.schedule.schedule_time.Value.Minutes, 
                                                                       x.schedule.schedule_time.Value.Seconds) // Chuyển đổi TimeSpan sang DateTime
                        })
                        .ToList();
            scheduleList = scheduleList.OrderBy(x => x.id).ToList();
            return Json(scheduleList, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetRoomSeat(int scheduleId, int displaydateId, int movieId)
        {
            var roomSeatsList = db.room_schedule_detail
                .Where(x => x.schedule_detail.schedule_id == scheduleId &&
                            x.schedule_detail.movie_display_date.display_date_id == displaydateId &&
                            x.schedule_detail.movie_display_date.movie_id == movieId)
                .Select(x => new
                {
                    RoomId = x.room.id,
                    RoomName = x.room.room_name,
                    Seats = x.seat_status.OrderBy(seat => seat.seat_id).Select(seat => new
                    {
                        SeatId = seat.seat_id,
                        SeatCol = seat.seat.seat_column,
                        SeatRow = seat.seat.seat_row,
                        SeatStt = seat.is_booked,   
                        SeatPrice = seat.seat.price
                    })
                    .ToList()
                })
                .ToList();

            return Json(roomSeatsList, JsonRequestBehavior.AllowGet);
        }

        public ActionResult MovieDetail()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
            var currentUser = (user)Session["user"];

            var segments = System.Web.HttpContext.Current.Request.Url.Segments;
            var movieIdSegment = segments[segments.Length - 1].TrimEnd('/');
            var movieId = int.Parse(movieIdSegment);

            var movieRatings = db.star_rating
                                .Where(sr => sr.movie_id == movieId)
                                .GroupBy(sr => sr.movie_id)
                                .Select(x => new
                                {
                                    AverageRating = Math.Round((decimal)x.Average(sr => sr.rating_value), 1),
                                    RatingCount = x.Count()
                                })
                                .FirstOrDefault();

            var movieItem = db.movies.FirstOrDefault(x => x.id == movieId);

            if (movieRatings != null)
            {
                movieItem.rating = movieRatings.AverageRating;
                ViewBag.RatingCount = movieRatings.RatingCount;
                db.SaveChanges();
            }
            else
            {
                movieItem.rating = 0;
                ViewBag.RatingCount = 0;
                db.SaveChanges();
            }

            ViewBag.movieItem = movieItem;

            var movieActor = db.movie_actor.Where(x => x.movie_id == movieId).ToList();
            ViewBag.movieActor = movieActor;

            var movieDateList = db.movie_display_date
                        .Where(x => x.movie_id == movieId)
                        .ToList();
            ViewBag.movieDateList = movieDateList;
            return View();
        }

        [HttpPost]
        public ActionResult MovieRating(int movieId, decimal rating)
        {
            var code = new { success = false, code = -1, averageRating = 0m, ratingCount = 0 };
            var currentUser = (user)Session["user"];
            var existMovies = db.invoices
                .Where(x => x.user_id == currentUser.id &&
                            x.room_schedule_detail.schedule_detail.movie_display_date.movie_id == movieId &&
                            x.room_schedule_detail.schedule_detail.movie_display_date.display_date.display_date1 <= DateTime.Now)
                .ToList();

            var hasWatchedMovie = existMovies.Any(x =>
                (x.room_schedule_detail.schedule_detail.movie_display_date.display_date.display_date1.Value.Add((TimeSpan)x.room_schedule_detail.schedule_detail.end_time) <= DateTime.Now));

            if (!hasWatchedMovie)
            {
                return Json(new { success = false, code = 0});
            }

            var ratingValue = db.star_rating.FirstOrDefault(x => x.movie_id == movieId && x.user_id == currentUser.id);
            if (ratingValue == null)
            {
                var newRating = new star_rating();
                newRating.movie_id = movieId;
                newRating.user_id = currentUser.id;
                newRating.rating_value = rating;
                db.star_rating.Add(newRating);
                db.SaveChanges();
                var movieRatings = db.star_rating
                                    .Where(sr => sr.movie_id == movieId)
                                    .GroupBy(sr => sr.movie_id)
                                    .Select(x => new
                                    {
                                        AverageRating = Math.Round((decimal)x.Average(sr => sr.rating_value), 1),
                                        RatingCount = x.Count()
                                    })
                                    .FirstOrDefault();

                if (movieRatings != null)
                {
                    code = new { success = true, code = 1, averageRating = movieRatings.AverageRating, ratingCount = movieRatings.RatingCount };
                }
            }
            
            return Json(code);
        }
        public ActionResult MovieType()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);

            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;


            var segments = System.Web.HttpContext.Current.Request.Url.Segments;
            var typeIdSegment = segments[segments.Length - 1].TrimEnd('/');
            int? typeId = null;
            int? countryId = null;
            int? yearValue = null;
            bool? movieStatus = null;
            bool? movieSelect = null;

            if (!string.IsNullOrEmpty(typeIdSegment))
            {
                int mtIndex = typeIdSegment.IndexOf("mt");
                int mcIndex = typeIdSegment.IndexOf("mc");
                int myIndex = typeIdSegment.IndexOf("my");
                int stIndex = typeIdSegment.IndexOf("st");
                int ssIndex = typeIdSegment.IndexOf("ss");

                // Kiểm tra xem "mt" có tồn tại và nằm ở vị trí đầu tiên
                if (mtIndex == 0)
                {
                    // Lấy chuỗi con sau "mt"
                    string typeIdStr = typeIdSegment.Substring(mtIndex + 2);

                    // Kiểm tra xem có "mc" trong chuỗi typeIdStr hay không
                    int mcIndexInTypeIdStr = typeIdStr.IndexOf("mc");

                    // Nếu có "mc", lấy typeId từ đầu chuỗi typeIdStr đến "mc"
                    if (mcIndexInTypeIdStr >= 0)
                    {
                        typeIdStr = typeIdStr.Substring(0, mcIndexInTypeIdStr);
                    }

                    int parsedTypeId;
                    if (int.TryParse(typeIdStr, out parsedTypeId))
                    {
                        typeId = parsedTypeId;
                    }
                }

                if (mcIndex > mtIndex)
                {
                    string countryIdStr = typeIdSegment.Substring(mcIndex + 2);

                    int myIndexInTypeIdStr = countryIdStr.IndexOf("my");

                    if (myIndexInTypeIdStr >= 0)
                    {
                        countryIdStr = countryIdStr.Substring(0, myIndexInTypeIdStr);
                    }

                    int parsedCountryId;
                    if (int.TryParse(countryIdStr, out parsedCountryId))
                    {
                        countryId = parsedCountryId;
                    }
                }

                if (myIndex > mcIndex)
                {
                    string yearIdStr = typeIdSegment.Substring(myIndex + 2);

                    int stIndexInTypeIdStr = yearIdStr.IndexOf("st");

                    if (stIndexInTypeIdStr >= 0)
                    {
                        yearIdStr = yearIdStr.Substring(0, stIndexInTypeIdStr);
                    }

                    int parsedYearValue;
                    if (int.TryParse(yearIdStr, out parsedYearValue))
                    {
                        yearValue = parsedYearValue;
                    }
                }

                if (stIndex > myIndex)
                {
                    string movieSttStr = typeIdSegment.Substring(stIndex + 2);

                    int ssIndexInTypeIdStr = movieSttStr.IndexOf("ss");

                    if (ssIndexInTypeIdStr >= 0)
                    {
                        movieSttStr = movieSttStr.Substring(0, ssIndexInTypeIdStr);
                    }

                    if (movieSttStr == "1") movieStatus = true;
                    else if (movieSttStr == "0") movieStatus = false;
                }

                if (ssIndex > stIndex)
                {
                    string movieSelectStr = typeIdSegment.Substring(ssIndex + 2);

                    if (movieSelectStr == "1") movieSelect = true;
                    else if (movieSelectStr == "0") movieSelect = false;
                }
            }

            if (typeId != null)
            {
                movielist = movielist.Where(x => x.type_id == typeId).ToList();
                var typeName = db.movie_type.FirstOrDefault(x => x.id == typeId).movie_type1;
                ViewBag.TypeName = typeName;
                ViewBag.SelectedTypeId = typeId;
            }

            if (countryId != null)
            {
                movielist = movielist.Where(x => x.country_id == countryId).ToList();
                var countryName = db.countries.FirstOrDefault(x => x.id == countryId).country_name;
                ViewBag.CountryName = countryName;
                ViewBag.SelectedCountryId = countryId;
            }

            if (yearValue != null)
            {
                movielist = movielist.Where(x => x.release_date.Value.Year == yearValue).ToList();
                ViewBag.MovieYear = yearValue;
            }

            if (movieStatus != null)
            {
                movielist = movielist.Where(x => x.movie_status == movieStatus).ToList();
                ViewBag.MovieStatus = (movieStatus == true) ? "1" : "0";
                ViewBag.StatusName = (movieStatus == true) ? "Đang chiếu" : "Sắp chiếu";
            }

            if (movieSelect != null)
            {
                if (movieSelect == true)
                {
                    movielist = movielist.OrderByDescending(x => x.release_date).ToList();
                }
                else
                {
                    movielist = movielist.OrderByDescending(x => x.rating).ToList();
                }

                ViewBag.MovieSelect = (movieSelect == true) ? "1" : "0";
                ViewBag.SelectName = (movieSelect == true) ? "Mới nhất" : "Đánh giá tốt nhất";
            }

            if (!movielist.Any())
            {
                TempData["MovieNotExist"] = "Không tìm thấy phim!";
            }

            ViewBag.MovieListType = movielist;

            var movieType = db.movie_type.ToList();
            ViewBag.movieType = movieType;

            var movieCountry = db.countries.ToList();
            ViewBag.movieCountry = movieCountry;


            return View();
        }

        public ActionResult DirectorList()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
        
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
        
            var countryList = db.countries.ToList();
            ViewBag.CountryList = countryList;
        
            var directorList = db.directors.ToList();
        
            var segments = System.Web.HttpContext.Current.Request.Url.Segments;
            var countryIdSegment = segments[segments.Length - 1].TrimEnd('/');
            int? countryId = null;
        
            if (!string.IsNullOrEmpty(countryIdSegment))
            {
                int parsedCountryId;
                if (int.TryParse(countryIdSegment, out parsedCountryId))
                {
                    countryId = parsedCountryId;
                }
            }
        
            if (countryId != null)
            {
                directorList = directorList.Where(x => x.country_id == countryId).ToList();
                var countryName = db.countries.FirstOrDefault(x => x.id == countryId).country_name;
                ViewBag.CountryName = countryName;
                ViewBag.SelectedCountryId = countryId;
            }
            if (!directorList.Any())
            {
                TempData["DirectorNotExist"] = "Không tìm thấy đạo diễn!";
            }
            ViewBag.DirectorList = directorList;
            return View();
        }
        
        public ActionResult DirectorDetail(int? id)
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
        
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
        
            if (id.HasValue)
            {
                var directorItem = db.directors.FirstOrDefault(x => x.id == id.Value);
                ViewBag.DirectorItem = directorItem;
        
                var movieDirector = db.movies.Where(x => x.director_id == id.Value).ToList();
                ViewBag.MovieDirector = movieDirector;
            }
            else
            {
                ViewBag.ActorItem = null;
                ViewBag.MovieDirector = null;
            }
        
            return View();
        }
        
        public ActionResult ActorList()
        {
            
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
        
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
        
            var countryList = db.countries.ToList();
            ViewBag.CountryList = countryList;
        
            var actorList = db.actors.ToList();
        
            var segments = System.Web.HttpContext.Current.Request.Url.Segments;
            var countryIdSegment = segments[segments.Length - 1].TrimEnd('/');
            int? countryId = null;
        
            if (!string.IsNullOrEmpty(countryIdSegment)) {
                int parsedCountryId;
                if (int.TryParse(countryIdSegment, out parsedCountryId))
                {
                    countryId = parsedCountryId;
                }
            }
        
            if (countryId != null)
            {
                actorList = actorList.Where(x => x.country_id == countryId).ToList();
                var countryName = db.countries.FirstOrDefault(x => x.id == countryId).country_name;
                ViewBag.CountryName = countryName;
                ViewBag.SelectedCountryId = countryId;
            }
            if (!actorList.Any())
            {
                TempData["ActorNotExist"] = "Không tìm thấy diễn viên!";
            }
            ViewBag.ActorList = actorList;
        
            return View();
        }
        
        public ActionResult ActorDetail(int? id)
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
        
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
        
            if (id.HasValue)
            {
                var actorItem = db.actors.FirstOrDefault(x => x.id == id.Value);
                ViewBag.ActorItem = actorItem;
        
                var movieActor = db.movie_actor.Where(x => x.actor_id == id.Value).Select(x => x.movy).ToList();
                ViewBag.MovieActor = movieActor;
            }
            else
            {
                ViewBag.ActorItem = null;
                ViewBag.MovieActor = null;
            }
        
            return View();
        }

        public class MovieByMonth
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public movy Movie { get; set; }
        }

        public ActionResult BestMovie()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;

            var moviesByMonth = movielist
                .GroupBy(m => new { m.release_date.Value.Year, m.release_date.Value.Month })
                .Select(g => new MovieByMonth
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Movie = g.OrderByDescending(m => m.rating).FirstOrDefault()
                })
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Month)
                .ToList();
            foreach (var item in moviesByMonth)
            {
            }
            ViewBag.BestMovieList = moviesByMonth;
            return View();
        }

        //[HttpGet]
        //public ActionResult GetDiscount()
        //{
        //    var currentUser = (user)Session["user"];
        //    DateTime currentDate = DateTime.Now;

        //    if (currentUser.created.Value.AddMonths(1) <= currentDate)
        //    {
        //        var invoice = db.invoices.Where(x => x.user_id == currentUser.id).ToList();
        //        var invoicePerYear = invoice
        //            .GroupBy(t => t.day_create.Value.Year)
        //            .Select(g => g.Key)
        //            .ToList();

        //        if (invoicePerYear.Contains(currentDate.Year))
        //        {
        //            var discountList = db.user_discount.Where(x => x.user_id == currentUser.id && x.start_date.Value.Month == currentDate.Month && x.discount_status == false)
        //                                       .Select(y => new
        //                                       {
        //                                           disId = y.discount_id,
        //                                           disStt = y.discount_status,
        //                                           disTitle = y.discount.title,
        //                                           disItem = y.discount.discount1,
        //                                           disDes = y.discount.dis_description,
        //                                           disEnd = y.end_date,
        //                                           disStart = y.start_date
        //                                       })
        //                                       .ToList();

        //            return Json(discountList, JsonRequestBehavior.AllowGet);
        //        }
        //        else
        //        {
        //            return Json(new { success = false }, JsonRequestBehavior.AllowGet);
        //        }
        //    }
        //    else
        //    {
        //        return Json(new { success = false}, JsonRequestBehavior.AllowGet);
        //    }
        //}

        public ActionResult MovieReview() 
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);

            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
            return View();
        }
        public ActionResult PaymentFail()
        {
            if (Session["user"] == null)
            {
                return RedirectToAction("SignIn", "Home", new { area = "" });
            }
            var currentUser = (user)Session["user"];
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
            return View();
        }
        public ActionResult PaymentSuccess()
        {
            if (Session["user"] == null)
            {
                return RedirectToAction("SignIn", "Home", new { area = "" });
            }
            var currentUser = (user)Session["user"];
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            movielist = movielist.OrderByDescending(m => m.release_date).ToList();
            ViewBag.MovieList = movielist;
            return View();
        }

        public ActionResult VnPayReturn()
        {
            if (Request.QueryString.Count > 0)
            {
                string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"]; //Chuoi bi mat
                var vnpayData = Request.QueryString;
                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (string s in vnpayData)
                {
                    //get all querystring data
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(s, vnpayData[s]);
                    }
                }

                long invoiceId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
                var invoiceItem = db.invoices.FirstOrDefault(x => x.id == (int)invoiceId);
                long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                String vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
                String TerminalID = Request.QueryString["vnp_TmnCode"];
                long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
                String bankCode = Request.QueryString["vnp_BankCode"];
                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);
                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                    {
                        //Thanh toan thanh cong
                        TempData["Message"] = "Giao dịch được thực hiện thành công. Cảm ơn quý khách đã sử dụng dịch vụ";
                        invoiceItem.invoice_status = true;
                        var notification = new notification();
                        notification.user_id = invoiceItem.user_id;
                        notification.content = "Bạn ơi, mua vé thành công rồi nè!";
                        notification.sub_content = invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.movy.title + ", Suất: " +
                                                   invoiceItem.room_schedule_detail.schedule_detail.schedule.schedule_time.Value.ToString(@"hh\:mm") + ", " +
                                                   invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.display_date.display_date1.Value.ToString("dd/MM/yyyy");
                        notification.status = false;
                        notification.date_create = DateTime.Now;
                        db.notifications.Add(notification);

                        if (invoiceItem.discount != null)
                        {
                            var usedDiscount = db.discounts.FirstOrDefault(x => x.id == invoiceItem.id);
                            foreach (var item in usedDiscount.user_discount)
                            {
                                if (invoiceItem.user_id == item.user_id)
                                {
                                    item.discount_status = true;
                                }
                            }
                        }

                        db.SaveChanges();

                        

                        var adminList = db.users.Where(x => x.user_type == 2).ToList();
                        var adminNotice = new notification();
                        foreach (var admin in adminList)
                        {
                            adminNotice.user_id = admin.id;
                            adminNotice.content = "Vé";
                            adminNotice.sub_content = "Người dùng " + invoiceItem.user.full_name + " đã đặt vé thành công!";
                            adminNotice.date_create = DateTime.Now;
                            adminNotice.status = false;
                            db.notifications.Add(adminNotice);
                            db.SaveChanges();
                        }
                        

                        _hubContext.Clients.All.broadcastNotification(adminNotice.content, adminNotice.sub_content);

                        string invoiceDataJson = JsonConvert.SerializeObject(invoiceItem.id); 
                        CreateQrCode(invoiceDataJson, invoiceItem);
                        SendMail(invoiceItem);
                        return RedirectToAction("PaymentSuccess", "BookTicket", new { area = "User" });
                    }
                    else
                    {
                        //Thanh toan khong thanh cong. Ma loi: vnp_ResponseCode
                        TempData["Message"] = "Có lỗi xảy ra trong quá trình xử lý.Mã lỗi: " + vnp_ResponseCode;
                        var ticketList = invoiceItem.tickets.ToList();
                        List<seat_status> chosenSeat = new List<seat_status>();
                        foreach (var ticket in ticketList)
                        {
                            chosenSeat.AddRange(ticket.seat.seat_status.Where(x => x.room_schedule_detail_id == invoiceItem.room_schedule_detail_id && x.is_booked == true && x.seat_id == ticket.seat_id));
                            db.tickets.Remove(ticket);
                            db.SaveChanges();
                        }
                            foreach (var seat in chosenSeat)
                            {
                                seat.is_booked = false;
                                seat.hold_until = null;
                            }
                        db.invoices.Remove(invoiceItem);
                        db.SaveChanges();

                        return RedirectToAction("PaymentFail", "BookTicket", new { area = "User" });
                        //log.InfoFormat("Thanh toan loi, OrderId={0}, VNPAY TranId={1},ResponseCode={2}", orderId, vnpayTranId, vnp_ResponseCode);
                    }
                }
            }
            return View();
        }
        public void CreateQrCode(string invoiceDataJson, invoice invoiceItem)
        {
            using (QRCodeGenerator qRCodeGenerator = new QRCodeGenerator())
            {
                try
                {
                    QRCodeData qRCodeData = qRCodeGenerator.CreateQrCode(invoiceDataJson, QRCodeGenerator.ECCLevel.Q);
                    QRCode qRCode = new QRCode(qRCodeData);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (Bitmap bitmap = qRCode.GetGraphic(20))
                        {
                            bitmap.Save(ms, ImageFormat.Png);
                            invoiceItem.qrcode_image = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                        }
                    }

                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
            }
        }

        public void SendMail(invoice invoiceItem)
        {
            // Gửi mail khi thanh toán thành công
            var MovieImage = invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.movy.url_image;
            var MovieTitle = invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.movy.title;
            var MovieSchedule = invoiceItem.room_schedule_detail.schedule_detail.schedule.schedule_time.Value.ToString(@"hh\:mm");
            var MovieDayOfWeek = invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.display_date.display_date1.Value.DayOfWeek;
            var MovieDate = invoiceItem.room_schedule_detail.schedule_detail.movie_display_date.display_date.display_date1.Value.ToString("dd/MM/yyyy");

            var TicketSeat = "";
            foreach (var ticketItem in invoiceItem.tickets)
            {
                //if (ticketItem.seat.room_id == newInvoice.room_schedule_detail.room_id)
                {
                    TicketSeat += ticketItem.seat.seat_column + ticketItem.seat.seat_row;
                    TicketSeat += " ";
                }
            }

            byte[] qrCodeImageBytes = null;

            if (invoiceItem.qrcode_image != null)
                qrCodeImageBytes = Convert.FromBase64String(invoiceItem.qrcode_image.Replace("data:image/png;base64,", ""));

            //var QrCode = "";
            //if (invoiceItem.qrcode_image != null)
            //{
            //    QrCode = invoiceItem.qrcode_image;
            //}

            string contentInvoice = System.IO.File.ReadAllText(Server.MapPath("~/Areas/User/Common/invoice.html"));
            contentInvoice = contentInvoice.Replace("{{MovieImage}}", MovieImage);
            contentInvoice = contentInvoice.Replace("{{MovieTitle}}", MovieTitle);
            contentInvoice = contentInvoice.Replace("{{MovieSchedule}}", MovieSchedule);
            contentInvoice = contentInvoice.Replace("{{MovieDayOfWeek}}", MovieDayOfWeek.ToString());
            contentInvoice = contentInvoice.Replace("{{MovieDate}}", MovieDate);
            contentInvoice = contentInvoice.Replace("{{TicketSeat}}", TicketSeat);
            //contentInvoice = contentInvoice.Replace("{{QrCode}}", QrCode);
            contentInvoice = contentInvoice.Replace("{{InvoiceId}}", invoiceItem.id.ToString());
            contentInvoice = contentInvoice.Replace("{{TotalMoney}}", invoiceItem.total_money.ToString());
            //if (qrCodeImageBytes != null)
            //{
            //    contentInvoice = contentInvoice.Replace("{{QrCode}}", "<img src=\"cid:qrcode_image\" />");
            //}
            CinemaWeb.Areas.User.Common.Common.SendMailWithQRCode("Ohayou Cinema", "Chúc mừng bạn đã đặt vé thành công!", contentInvoice, invoiceItem.user.email, qrCodeImageBytes);
            System.Diagnostics.Debug.WriteLine(contentInvoice);
        }

        [HttpPost]
        public ActionResult GetInvoice()
        {
            var code = new { success = false, code = -1, Url = "" };
            string jsonData;
            using (var reader = new StreamReader(Request.InputStream))
            {
                jsonData = reader.ReadToEnd();
            }

            JObject jsonObject = JObject.Parse(jsonData);

            int movieId = (int)jsonObject["movieId"];
            int displaydateId = (int)jsonObject["displaydateId"];
            int scheduleId = (int)jsonObject["scheduleId"];
            int roomId = (int)jsonObject["roomId"];
            int? discountId = (int?)jsonObject["discountId"];
            JArray chosenSeatsArray = (JArray)jsonObject["chosenSeats"];
            int[] chosenSeats = chosenSeatsArray.ToObject<int[]>();

            if (Session["user"] == null)
            {
                return RedirectToAction("SignIn", "Home", new { area = "" });
            }

            var currentUser = (user)Session["user"];
            var newInvoice = new invoice();
            newInvoice.user_id = currentUser.id;
            newInvoice.invoice_status = false;
            newInvoice.room_schedule_detail_id = GetRoomScheduleDetailId(roomId, displaydateId, scheduleId, movieId);
            newInvoice.day_create = DateTime.Now;
            //newInvoice.day_create = DateTime.UtcNow;
            newInvoice.total_ticket = chosenSeats.Length;

            db.invoices.Add(newInvoice);
            db.SaveChanges();
            
            var total_money = CalculateTotalMoney(chosenSeats);
            var discountItem = new discount();

            if (discountId != null) 
            {
                 discountItem = db.discounts.FirstOrDefault(x => x.id == discountId);

                newInvoice.total_money = ((int?)(total_money - discountItem.discount1 * total_money));
                newInvoice.discount_id = discountId;
                //foreach (var item in discountItem.user_discount)
                //{
                //    if (currentUser.id == item.user_id)
                //    {
                //        item.discount_status = true;
                //    }
                //}
            }
            else
            {
                newInvoice.total_money = total_money;
            }

            foreach (var seatId in chosenSeats)
            {
                var seatStatus = db.seat_status.FirstOrDefault(s => s.seat_id == seatId && s.room_schedule_detail_id == newInvoice.room_schedule_detail_id);
                if (seatStatus != null)
                {
                    seatStatus.is_booked = true;
                    seatStatus.hold_until = null;
                }
            }
            db.SaveChanges();
            AddTicket(chosenSeats, newInvoice.id);

            var url = UrlPayment(newInvoice.id, currentUser.id);
            code = new { success = true, code = 1, Url = url };
            return Json(code);
        }

        public int GetRoomScheduleDetailId(int roomId, int displaydateId, int scheduleId, int movieId)
        {
            var roomScheduleDetail = db.room_schedule_detail
                                        .FirstOrDefault(x => x.room_id == roomId
                                                          && x.schedule_detail.movie_display_date.display_date_id == displaydateId
                                                          && x.schedule_detail.schedule_id == scheduleId
                                                          && x.schedule_detail.movie_display_date.movie_id == movieId);
            if (roomScheduleDetail != null)
            {
                return roomScheduleDetail.id;
            }
            throw new Exception("Không tìm thấy thông tin phòng và lịch chiếu tương ứng.");
        }

        // Hàm để tính tổng tiền dựa trên số lượng ghế và giá vé
        public int? CalculateTotalMoney(int[] chosenSeats)
        {
            int? totalPrice = 0;
            
            foreach (var seatId in chosenSeats)     
            {
                var seat = db.seats.FirstOrDefault(s => s.id == seatId);
                if (seat != null)
                {
                    totalPrice += seat.price; 
                }
            }
            return totalPrice;
        }

        public void AddTicket(int[] chosenSeats, int invoiceId)
        {
            var invoiceItem = db.invoices.FirstOrDefault(x => x.id == invoiceId);
                var newTicket = new ticket();
                foreach (var seatId in chosenSeats)
                {
                    var seat = db.seats.FirstOrDefault(s => s.id == seatId);
                    if (seat != null)
                    {
                        newTicket.invoice_id = invoiceId;
                        newTicket.seat_id = seat.id;
                        newTicket.ticket_status = true;
                        newTicket.ticket_price = seat.price;
                        db.tickets.Add(newTicket);
                        db.SaveChanges();
                    }
                }
        }
        [HttpPost]
        public ActionResult HoldSeat()
        {
            string jsonData;
            using (var reader = new StreamReader(Request.InputStream))
            {
                jsonData = reader.ReadToEnd();
            }

            JObject jsonObject = JObject.Parse(jsonData);

            int movieId = (int)jsonObject["movieId"];
            int displaydateId = (int)jsonObject["displaydateId"];
            int scheduleId = (int)jsonObject["scheduleId"];
            int roomId = (int)jsonObject["roomId"];
            JArray chosenSeatsArray = (JArray)jsonObject["chosenSeats"];
            int[] chosenSeats = chosenSeatsArray.ToObject<int[]>();

            var selectedSeat = db.seat_status.Where(x => x.room_schedule_detail.room_id == roomId &&
                                                         x.room_schedule_detail.schedule_detail.schedule_id == scheduleId &&
                                                         x.room_schedule_detail.schedule_detail.movie_display_date.display_date_id == displaydateId &&
                                                         x.room_schedule_detail.schedule_detail.movie_display_date.movie_id == movieId)
                                             .ToList();

            var currentUser = (user)Session["user"];
            DateTime holdUntil = DateTime.Now.AddMinutes(5);
            foreach (var seatId in chosenSeats)
           {
                var seatItem = selectedSeat.FirstOrDefault(x => x.seat_id == seatId);
                if (seatItem != null)
                {
                    if (seatItem.is_booked == false)
                    {
                        seatItem.is_booked = true;
                        seatItem.hold_until = holdUntil;
                        seatItem.user_id = currentUser.id;
                        db.SaveChanges();
                    }
                    else
                    {
                        if (seatItem.user_id != currentUser.id)
                            return Json(new { success = false, message = "Ghế đã được chọn. Vui lòng chọn ghế khác!" });
                    }
                }
           }
            return Json(new { success = true, message = "Thành công!" });
        }

        [HttpPost]
        public ActionResult RemoveHoldSeat()
        {
            string jsonData;
            using (var reader = new StreamReader(Request.InputStream))
            {
                jsonData = reader.ReadToEnd();
            }

            JObject jsonObject = JObject.Parse(jsonData);

            int movieId = (int)jsonObject["movieId"];
            int displaydateId = (int)jsonObject["displaydateId"];
            int scheduleId = (int)jsonObject["scheduleId"];
            int roomId = (int)jsonObject["roomId"];
            JArray chosenSeatsArray = (JArray)jsonObject["chosenSeats"];
            int[] chosenSeats = chosenSeatsArray.ToObject<int[]>();

            var selectedSeat = db.seat_status.Where(x => x.room_schedule_detail.room_id == roomId &&
                                                         x.room_schedule_detail.schedule_detail.schedule_id == scheduleId &&
                                                         x.room_schedule_detail.schedule_detail.movie_display_date.display_date_id == displaydateId &&
                                                         x.room_schedule_detail.schedule_detail.movie_display_date.movie_id == movieId)
                                             .ToList();
            foreach (var seatId in chosenSeats)
            {
                var seatItem = selectedSeat.FirstOrDefault(x => x.seat_id == seatId);
                if (seatItem != null)
                {
                    if ((bool)seatItem.is_booked)
                    {
                        seatItem.is_booked = false;
                        seatItem.hold_until = null;
                        db.SaveChanges();
                    }
                    //else
                    //{
                    //    return Json(new { success = false, message = "Ghế đã được chọn. Vui lòng chọn ghế khác!" });
                    //}
                }
            }
            return Json(new { success = true, message = "Thành công!" });
        }
        #region
        //Thanh toán vnpay
        public string UrlPayment(int invoiceId, int userId)
        {
            string paymentUrl = string.Empty;
            var invoiceDetail = db.invoices.FirstOrDefault(x => x.id == invoiceId && x.user_id == userId);

            string vnp_Returnurl = ConfigurationManager.AppSettings["vnp_Returnurl"]; //URL nhan ket qua tra ve 
            string vnp_Url = ConfigurationManager.AppSettings["vnp_Url"]; //URL thanh toan cua VNPAY 
            string vnp_TmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"]; //Ma định danh merchant kết nối (Terminal Id)
            string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"]; //Secret Key
            long invoicePrice = (long)invoiceDetail.total_money * 100;
            VnPayLibrary vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", invoicePrice.ToString()); //Số tiền thanh toán. Số tiền không mang các ký tự phân tách thập phân, phần nghìn, ký tự tiền tệ. Để gửi số tiền thanh toán là 100,000 VND (một trăm nghìn VNĐ) thì merchant cần nhân thêm 100 lần (khử phần thập phân), sau đó gửi sang VNPAY là: 10000000

            vnpay.AddRequestData("vnp_CreateDate", invoiceDetail.day_create.Value.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            string ip = Utils.GetIpAddress();
            vnpay.AddRequestData("vnp_IpAddr", ip);
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang:" + invoiceDetail.id);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", invoiceDetail.id.ToString()); // Mã tham chiếu của giao dịch tại hệ thống của merchant. Mã này là duy nhất dùng để phân biệt các đơn hàng gửi sang VNPAY. Không được trùng lặp trong ngày


            //Add Params of 2.1.0 Version
            //Billing

            paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            //log.InfoFormat("VNPAY URL: {0}", paymentUrl);
            return paymentUrl;
        }
        #endregion
    }
}
