using GG_Shop_v3.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GG_Shop_v3.Controllers
{
    public class AccountController : Controller
    {
        private readonly DataContext db = new DataContext();

        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (Request.IsAjaxRequest())
                {
                    var errors = ModelState.Where(x => x.Value.Errors.Any())
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return Json(new { success = false, errors });
                }

                return View(model);
            }

            var user = await db.users.FirstOrDefaultAsync(x => x.Email == model.Email);

            if (user == null || user.Password != model.Password)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = false,
                        errors = new { _global = new[] { "Email hoặc mật khẩu không đúng" } }
                    });
                }

                return View(model);
            }

            // Lưu session
            Session["User"] = new { user.Id, user.Email, user.Full_Name };
            Session["RememberMe"] = model.RememberMe;

            // Nếu muốn lưu cookie:
            if (model.RememberMe)
            {
                HttpCookie cookie = new HttpCookie("UserEmail", user.Email);
                cookie.Expires = DateTime.Now.AddDays(7);
                Response.Cookies.Add(cookie);
            }

            if (Request.IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("Index", "Home")
                });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("User");
            return RedirectToAction("Login", "Account");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}