using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using CinemaWeb.Models;
using CinemaWeb.Areas.Admin.Helper;
using System.Data.Entity.Validation;
using System.Threading.Tasks;

namespace CinemaWeb.Areas.Admin.Controllers
{
    public class MoviesController : Controller
    {
        private Cinema_Web_Entities db = new Cinema_Web_Entities();

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
        // GET: Admin/Movies
        public ActionResult Index()
        {
            List<movy> movielist = db.movies.ToList();
            GetMovieStatus(movielist);
            db.SaveChanges();
            var movies = db.movies.Include(m => m.country).Include(m => m.director).Include(m => m.movie_type);
            return View(movies.ToList());
        }

        [HttpGet]
        public ActionResult SearchMovie(string searchQuery)
        {
            var movies = db.movies
                .Where(m => m.title.Contains(searchQuery) ||
                            m.country.country_name.Contains(searchQuery) ||
                            m.movie_type.movie_type1.Contains(searchQuery))
                .Select(m => new
                {
                    m.id,
                    m.title,
                    country_name = m.country.country_name,
                    movie_type = m.movie_type.movie_type1,
                    release_date = m.release_date,
                    end_date = m.end_date,
                    movie_status = m.movie_status
                })
                .ToList();

            if (movies.Any())
            {
                var movieList = movies.GroupBy(m => new { m.id, m.title, m.country_name, m.movie_type, m.release_date, m.end_date, m.movie_status })
                                        .Select(g => new
                                        {
                                            g.Key.id,
                                            g.Key.title,
                                            g.Key.country_name,
                                            g.Key.movie_type,
                                            release_date = g.Key.release_date.Value.ToString("dd/MM/yyyy"),
                                            end_date = g.Key.end_date.Value.ToString("dd/MM/yyyy"),
                                            g.Key.movie_status
                                        }).ToList();
                return Json(movieList, JsonRequestBehavior.AllowGet);
            }
            return Json(new { success = false, message = "Không tìm thấy phim" }, JsonRequestBehavior.AllowGet);
        }

        // GET: Admin/Movies/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            movy movy = db.movies.Find(id);
            if (movy == null)
            {
                return HttpNotFound();
            }
            return View(movy);
        }

        // GET: Admin/Movies/Create
        public ActionResult Create()
        {
            ViewBag.country_id = new SelectList(db.countries, "id", "country_name");
            ViewBag.director_id = new SelectList(db.directors, "id", "director_name");
            ViewBag.type_id = new SelectList(db.movie_type, "id", "movie_type1");
            return View();
        }

        // POST: Admin/Movie/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "id,title,description,director_name,type_id,release_date,end_date,duration_minutes,country_id,created_at,movie_status,url_image,rating,url_trailer,url_large_image,ActorNames")] movy movy, HttpPostedFileBase fileImage, HttpPostedFileBase fileLargeImage)
        {
            if (ModelState.IsValid)
            {
                // Xử lý ảnh nhỏ
                if (fileImage != null && fileImage.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(fileImage.FileName);
                    string filePath = Path.Combine(Server.MapPath("~/uploads"), fileName);
                    fileImage.SaveAs(filePath);
                    movy.url_image = "https://localhost:44323/uploads/" + fileName;
                }

                // Xử lý ảnh lớn
                if (fileLargeImage != null && fileLargeImage.ContentLength > 0)
                {
                    string largeFileName = Path.GetFileName(fileLargeImage.FileName);
                    string largeFilePath = Path.Combine(Server.MapPath("~/uploads"), largeFileName);
                    fileLargeImage.SaveAs(largeFilePath);
                    movy.url_large_image = "https://localhost:44323/uploads/" + largeFileName;
                }

                // Check if the director exists, if not create a new director
                var director = db.directors.FirstOrDefault(d => d.director_name == movy.director_name);
                if (director == null)
                {
                    director = new director
                    {
                        director_name = movy.director_name,
                        title = "Đang cập nhật",
                        country_id = 1, 
                        director_img = "https://cdn.galaxycine.vn/media/2021/12/27/image-2021_1640588706930.png",
                        description = "Đang cập nhật"
                    };
                    db.directors.Add(director);
                    await db.SaveChangesAsync();
                }

                movy.director_id = director.id;

                try
                {
                    var movie = new movy
                    {
                        title = movy.title,
                        description = movy.description,
                        director_id = movy.director_id,
                        type_id = movy.type_id,
                        release_date = movy.release_date,
                        end_date = movy.end_date,
                        duration_minutes = movy.duration_minutes,
                        country_id = movy.country_id,
                        created_at = movy.created_at,
                        movie_status = movy.movie_status,
                        url_image = movy.url_image,
                        rating = movy.rating,
                        url_trailer = movy.url_trailer,
                        url_large_image = movy.url_large_image
                    };

                    db.movies.Add(movie);
                    await db.SaveChangesAsync();

                    
                    // Thêm diễn viên vào bảng movie_actor
                    if (movy.ActorNames != null && movy.ActorNames.Any())
                    {
                        foreach (var actorName in movy.ActorNames)
                        {
                            var actor = db.actors.FirstOrDefault(a => a.actor_name == actorName);
                            if (actor == null)
                            {
                                actor = new actor
                                {
                                    actor_name = actorName,
                                    title = "Đang cập nhật",
                                    description = "Đang cập nhật",
                                    country_id = 1, // You might want to set this to a valid default country_id or manage it according to your application logic
                                    actor_img = "https://cdn.galaxycine.vn/media/2021/12/27/image-2021_1640588706930.png"
                                };
                                db.actors.Add(actor);
                                await db.SaveChangesAsync();
                            }

                            var movieActor = new movie_actor
                            {
                                movie_id = movie.id,
                                actor_id = actor.id
                            };
                            db.movie_actor.Add(movieActor);
                        }
                        await db.SaveChangesAsync();
                    }
                    var displayDate = new List<display_date>();

                    foreach (var item in db.display_date)
                    {
                        if (item.display_date1 >= movie.release_date && item.display_date1 <= movie.end_date)
                        {
                            displayDate.Add(item);
                        }
                    }

                    foreach (var item in displayDate)
                    {
                        var movieDisplayDate = new movie_display_date
                        {
                            display_date_id = item.id,
                            movie_id = movie.id
                        };
                        db.movie_display_date.Add(movieDisplayDate);
                        await db.SaveChangesAsync();
                    }

                    return RedirectToAction("Index"); // Chuyển hướng đến trang chủ hoặc trang khác
                }
                catch (DbEntityValidationException ex)
                {
                    // Xử lý lỗi xác thực
                    var errorMessages = ex.EntityValidationErrors
                                          .SelectMany(x => x.ValidationErrors)
                                          .Select(x => x.ErrorMessage);
                    var fullErrorMessage = string.Join("; ", errorMessages);
                    ModelState.AddModelError("", fullErrorMessage);
                }
            }

            // Nếu ModelState không hợp lệ, populate lại ViewBag và trả về View
            ViewBag.country_id = new SelectList(db.countries, "id", "country_name", movy.country_id);
            ViewBag.type_id = new SelectList(db.movie_type, "id", "movie_type1", movy.type_id);
            return View(movy);
        }




        private dynamic UploadImage(HttpPostedFileBase file)
        {
            var uploadController = DependencyResolver.Current.GetService<UploadController>();
            var jsonResult = uploadController.UploadImage(file) as JsonResult;
            return jsonResult.Data;
        }
        private string ConvertToBase64(HttpPostedFileBase file)
        {
            using (var reader = new BinaryReader(file.InputStream))
            {
                var bytes = reader.ReadBytes(file.ContentLength);
                return "data:" + file.ContentType + ";base64," + Convert.ToBase64String(bytes);
            }
        }

        // GET: Admin/Movies/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            movy movy = db.movies.Find(id);
            if (movy == null)
            {
                return HttpNotFound();
            }
            // Populate the list of actors
            var actors = (from ma in db.movie_actor
                          join a in db.actors on ma.actor_id equals a.id
                          where ma.movie_id == id
                          select a.actor_name).ToList();

            ViewBag.Actors = actors;
            // Populate the selected actors for the movie
            ViewBag.country_id = new SelectList(db.countries, "id", "country_name", movy.country_id);
            ViewBag.director_id = new SelectList(db.directors, "id", "director_name", movy.director_id);
            ViewBag.type_id = new SelectList(db.movie_type, "id", "movie_type1", movy.type_id);
            return View(movy);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id,title,description,director_id,type_id,release_date,end_date,duration_minutes,country_id,created_at,movie_status,url_image,rating,url_trailer,url_large_image,ActorNames")] movy movy, HttpPostedFileBase fileImage, HttpPostedFileBase fileLargeImage)
        {
            if (ModelState.IsValid)
            {
                var existingMovie = db.movies.AsNoTracking().FirstOrDefault(m => m.id == movy.id);

                if (existingMovie != null)
                {
                    // Xử lý ảnh nhỏ
                    if (fileImage == null)
                    {
                        movy.url_image = existingMovie.url_image; // Giữ nguyên nếu không tải lên
                    }
                    else
                    {
                        string fileName = Path.GetFileName(fileImage.FileName);
                        string filePath = Path.Combine(Server.MapPath("~/uploads"), fileName);
                        fileImage.SaveAs(filePath);
                        movy.url_image = "https://localhost:44323/uploads/" + fileName;
                    }

                    // Xử lý ảnh lớn
                    if (fileLargeImage == null)
                    {
                        movy.url_large_image = existingMovie.url_large_image; // Giữ nguyên nếu không tải lên
                    }
                    else
                    {
                        string largeFileName = Path.GetFileName(fileLargeImage.FileName);
                        string largeFilePath = Path.Combine(Server.MapPath("~/uploads"), largeFileName);
                        fileLargeImage.SaveAs(largeFilePath);
                        movy.url_large_image = "https://localhost:44323/uploads/" + largeFileName;
                    }

                    try
                    {
                        db.Entry(movy).State = EntityState.Modified;
                        if (movy.ActorNames != null && movy.ActorNames.Any())
                        {
                            foreach (var actorName in movy.ActorNames)
                            {
                                var actor = db.actors.FirstOrDefault(a => a.actor_name == actorName);
                                if (actor == null)
                                {
                                    actor = new actor
                                    {
                                        actor_name = actorName,
                                        title = "Đang cập nhật",
                                        description = "Đang cập nhật",
                                        country_id = 1, // You might want to set this to a valid default country_id or manage it according to your application logic
                                        actor_img = "https://cdn.galaxycine.vn/media/2021/12/27/image-2021_1640588706930.png"
                                    };
                                    db.actors.Add(actor);
                                }

                                var movieActor = new movie_actor
                                {
                                    movie_id = existingMovie.id,
                                    actor_id = actor.id
                                };
                                db.movie_actor.Add(movieActor);
                            }
                        }
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        // Xử lý lỗi xác thực
                        var errorMessages = ex.EntityValidationErrors
                                              .SelectMany(x => x.ValidationErrors)
                                              .Select(x => x.ErrorMessage);
                        var fullErrorMessage = string.Join("; ", errorMessages);
                        ModelState.AddModelError("", fullErrorMessage);

                        // Populate lại ViewBag cho dropdown và trả về view
                        ViewBag.country_id = new SelectList(db.countries, "id", "country_name", movy.country_id);
                        ViewBag.director_id = new SelectList(db.directors, "id", "director_name", movy.director_id);
                        ViewBag.type_id = new SelectList(db.movie_type, "id", "movie_type1", movy.type_id);

                        return View(movy);
                    }
                }
            }

            ViewBag.country_id = new SelectList(db.countries, "id", "country_name", movy.country_id);
            ViewBag.director_id = new SelectList(db.directors, "id", "director_name", movy.director_id);
            ViewBag.type_id = new SelectList(db.movie_type, "id", "movie_type1", movy.type_id);
            return View(movy);
        }

        // GET: Admin/Movies/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            movy movy = db.movies.Find(id);
            if (movy == null)
            {
                return HttpNotFound();
            }
            return View(movy);
        }

        // POST: Admin/Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            movy movy = db.movies.Find(id);
            db.movies.Remove(movy);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
