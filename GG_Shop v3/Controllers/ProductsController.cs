using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using GG_Shop_v3.Models;

namespace GG_Shop_v3.Controllers
{
    public class ProductsController : Controller
    {
        private DataContext db = new DataContext();

        // GET: Products
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetProducts()
        {
            var products = db.products
                .Include(p => p.Category)
                .Include(p => p.Product_Images)
                .Include(p => p.Product_Sku)   // chính xác tên collection
                .ToList();

            var result = products.Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                Category = p.Category != null ? new { p.Category.Id, p.Category.Name } : null,

                Product_Images = (p.Product_Images ?? new List<Product_Image>())
                    .Select(i => new
                    {
                        i.Id,
                        Image_Url = string.IsNullOrEmpty(i.Image_Url) ? null : Url.Content(i.Image_Url),
                        i.Is_Main
                    }).ToList(),

                Product_Skus = (p.Product_Sku ?? new List<Product_Sku>())
                    .Select(s => new
                    {
                        s.Id,
                        s.Sku,
                        s.Color,
                        s.Size,
                        s.Price,
                        s.Quantity
                    }).ToList(),

                TotalQty = (p.Product_Sku?.Sum(s => s.Quantity)) ?? 0,
                MinPrice = (p.Product_Sku?.Any() ?? false) ? (p.Product_Sku.Min(s => s.Price)) : 0m,
                MaxPrice = (p.Product_Sku?.Any() ?? false) ? (p.Product_Sku.Max(s => s.Price)) : 0m,

                p.Status
            }).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // GET: Products/Details
        public ActionResult Details(int id)
        {
            ViewBag.Id = id;
            return View();
        }

        [HttpGet]
        public JsonResult DetailsProduct(int id)
        {
            var product = db.products
                .Include(p => p.Category)
                .Include(p => p.Product_Images)
                .Include(p => p.Product_Sku) // sửa tên collection
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            var imageList = (product.Product_Images ?? new List<Product_Image>())
                .Select(i => new
                {
                    i.Id,
                    Url = string.IsNullOrEmpty(i.Image_Url) ? null : Url.Content(i.Image_Url),
                    i.Is_Main
                })
                .ToList();

            var skuList = (product.Product_Sku ?? new List<Product_Sku>())
                .Select(s => new
                {
                    s.Id,
                    s.Sku,
                    s.Color,
                    s.Size,
                    Price = s.Price,
                    Quantity = s.Quantity,
                    Total = s.Price * s.Quantity
                })
                .ToList();

            var mainImgUrl = imageList.FirstOrDefault(i => i.Is_Main)?.Url ?? imageList.FirstOrDefault()?.Url;

            var json = new
            {
                product.Id,
                product.Title,
                product.Description,
                CategoryName = product.Category?.Name,
                MainImageUrl = mainImgUrl,
                Images = imageList,
                Skus = skuList,
                TotalQty = skuList.Sum(s => (int?)s.Quantity) ?? 0,
                MinPrice = skuList.Any() ? skuList.Min(s => s.Price) : 0m,
                MaxPrice = skuList.Any() ? skuList.Max(s => s.Price) : 0m,
                product.Status
            };

            return Json(json, JsonRequestBehavior.AllowGet);
        }

        // GET: Create Product (form)
        public ActionResult Create()
        {
            ViewBag.Category_Id = new SelectList(db.categories, "Id", "Name");
            return View();
        }

        [HttpGet]
        public JsonResult CreateProduct(string title, List<Product_Sku> skus, IEnumerable<HttpPostedFileBase> images, decimal? BasePrice, int? BaseQuantity)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Tên sản phẩm không được để trống." }, JsonRequestBehavior.AllowGet);
            }

            var titleLower = title.Trim().ToLower();
            if (db.products.Any(p => p.Title.ToLower() == titleLower))
            {
                return Json(new { success = false, message = "Tên sản phẩm đã tồn tại." }, JsonRequestBehavior.AllowGet);
            }

            using (var tran = db.Database.BeginTransaction())
            {
                try
                {
                    var product = new Product
                    {
                        Title = title.Trim(),
                        // gán thêm các thuộc tính khác nếu cần
                    };
                    db.products.Add(product);
                    db.SaveChanges();

                    // lưu skus
                    if (skus != null)
                    {
                        foreach (var s in skus)
                        {
                            bool isEmpty =
                                string.IsNullOrWhiteSpace(s.Sku)
                                && string.IsNullOrWhiteSpace(s.Color)
                                && string.IsNullOrWhiteSpace(s.Size)
                                && (s.Quantity == 0)
                                && (s.Price == 0m);

                            if (isEmpty) continue;
                            s.Product_Id = product.Id;
                            if ((s.Price == 0m || s.Price == null) && BasePrice.HasValue) s.Price = BasePrice.Value;
                            if ((s.Quantity == 0) && BaseQuantity.HasValue) s.Quantity = BaseQuantity.Value;
                            db.product_skus.Add(s);
                        }
                    }

                    // lưu ảnh
                    if (images != null)
                    {
                        var uploadFolder = Server.MapPath("~/uploads/products");
                        if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                        foreach (var file in images)
                        {
                            if (file == null || file.ContentLength <= 0) continue;
                            var ext = Path.GetExtension(file.FileName);
                            var unique = Guid.NewGuid().ToString("N") + ext;
                            var path = Path.Combine(uploadFolder, unique);

                            file.SaveAs(path);

                            db.product_images.Add(new Product_Image
                            {
                                Product_Id = product.Id,
                                Image_Url = "/uploads/products/" + unique,
                                Is_Main = false
                            });
                        }
                    }

                    db.SaveChanges();
                    tran.Commit();

                    var json = new
                    {
                        product.Id,
                        product.Title,
                        product.Description,
                        CategoryName = product.Category?.Name,
                        Images = db.product_images.Where(i => i.Product_Id == product.Id).Select(i => new
                        {
                            i.Id,
                            Url = string.IsNullOrEmpty(i.Image_Url) ? null : Url.Content(i.Image_Url),
                            i.Is_Main
                        }).ToList(),
                        Skus = db.product_skus.Where(s => s.Product_Id == product.Id).Select(s => new
                        {
                            s.Id,
                            s.Sku,
                            s.Color,
                            s.Size,
                            Price = s.Price,
                            Quantity = s.Quantity,
                            Total = s.Price * s.Quantity
                        }).ToList(),
                        product.Status,
                        redirectUrl = Url.Action("Details", "Products", new { id = product.Id })
                    };

                    return Json(new { success = true, data = json }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return Json(new { success = false, message = "Lỗi khi lưu: " + ex.Message }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        [HttpPost]
        public JsonResult CreateAjax()
        {
            try
            {
                // Lấy dữ liệu cơ bản từ form
                string title = Request["Title"];
                string description = Request["Description"];
                string status = Request["Status"];
                int categoryId = int.TryParse(Request["Category_Id"], out var cid) ? cid : 0;

                // Kiểm tra trùng tên
                if (db.products.Any(p => p.Title == title))
                {
                    return Json(new { success = false, message = "Tên sản phẩm đã tồn tại" });
                }

                // Tạo product
                var product = new Product
                {
                    Title = title,
                    Description = description,
                    Status = status,
                    Category_Id = categoryId
                };
                db.products.Add(product);
                db.SaveChanges(); // để có Id

                // Lưu ảnh upload
                var files = Request.Files;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var path = Path.Combine(Server.MapPath("~/Uploads/Products"), fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        file.SaveAs(path);

                        db.product_images.Add(new Product_Image
                        {
                            Product_Id = product.Id,
                            Image_Url = "/Uploads/Products/" + fileName,
                            Is_Main = false
                        });
                    }
                }

                // Lấy Colors và Sizes (nếu cần lưu riêng)
                var colorsJson = Request["Colors"];
                var sizesJson = Request["Sizes"];
                var colors = string.IsNullOrEmpty(colorsJson) ? new List<string>() :
                    Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(colorsJson);
                var sizes = string.IsNullOrEmpty(sizesJson) ? new List<string>() :
                    Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(sizesJson);

                // Lưu SKU từ bảng biến thể
                var keys = Request.Form.AllKeys.Where(k => k.StartsWith("skus[") && k.EndsWith("].Sku")).ToList();
                foreach (var key in keys)
                {
                    var prefix = key.Substring(0, key.IndexOf("].Sku") + 1); // skus[0], skus[1], ...
                    var sku = Request[prefix + ".Sku"];
                    var color = Request[prefix + ".Color"];
                    var size = Request[prefix + ".Size"];
                    var quantity = int.TryParse(Request[prefix + ".Quantity"], out var q) ? q : 0;
                    var price = decimal.TryParse(Request[prefix + ".Price"], out var p) ? p : 0;

                    db.product_skus.Add(new Product_Sku
                    {
                        Product_Id = product.Id,
                        Sku = sku,
                        Color = color,
                        Size = size,
                        Quantity = quantity,
                        Price = price
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, redirectUrl = Url.Action("Index") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi thêm sản phẩm: " + ex.Message });
            }
        }


        // GET: Products/Edit
        public ActionResult Edit(int id)
        {
            ViewBag.Id = id;
            return View(); // View không cần Model, chỉ cần Id để JS gọi API
        }

        [HttpGet]
        public JsonResult EditProduct(int id)
        {
            var product = db.products
                .Include(p => p.Category)
                .Include(p => p.Product_Images)
                .Include(p => p.Product_Sku)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            // Danh sách ảnh
            var imageList = (product.Product_Images ?? new List<Product_Image>())
                .Select(i => new
                {
                    i.Id,
                    Url = string.IsNullOrEmpty(i.Image_Url) ? null : Url.Content(i.Image_Url),
                    i.Is_Main
                }).ToList();

            // Danh sách SKU
            var skuList = (product.Product_Sku ?? new List<Product_Sku>())
                .Select(s => new
                {
                    s.Id,
                    s.Sku,
                    s.Color,
                    s.Size,
                    Price = s.Price,
                    Quantity = s.Quantity
                }).ToList();

            // Danh sách danh mục
            var categories = db.categories
                .Select(c => new { c.Id, c.Name })
                .ToList();

            // Danh sách trạng thái cố định
            var statusList = new[] {
        "Đang bán",
        "Chưa bán",
        "Đã hết"
    };

            // Ảnh đại diện
            var mainImgUrl = imageList.FirstOrDefault(i => i.Is_Main)?.Url
                             ?? imageList.FirstOrDefault()?.Url;

            // JSON trả về
            var json = new
            {
                product.Id,
                product.Title,
                product.Description,
                product.Category_Id,
                CategoryName = product.Category?.Name,
                Categories = categories,
                Status = product.Status,
                StatusList = statusList,
                MainImageUrl = mainImgUrl,
                Images = imageList,
                Skus = skuList,
                TotalQty = skuList.Sum(s => (int?)s.Quantity) ?? 0,
                MinPrice = skuList.Any() ? skuList.Min(s => s.Price) : 0m,
                MaxPrice = skuList.Any() ? skuList.Max(s => s.Price) : 0m
            };

            return Json(json, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult EditAjax()
        {
            try
            {
                int id = int.Parse(Request["Id"]);
                string title = Request["Title"];
                string description = Request["Description"];
                int categoryId = int.Parse(Request["Category_Id"]);
                string status = Request["Status"];

                // Tìm sản phẩm cần sửa
                var product = db.products.Include(p => p.Product_Images).Include(p => p.Product_Sku).FirstOrDefault(p => p.Id == id);
                if (product == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm." });

                // Cập nhật thông tin cơ bản
                product.Title = title;
                product.Description = description;
                product.Category_Id = categoryId;
                product.Status = status;

                // Xử lý ảnh cần xóa
                var deleteImageIds = Request.Form.GetValues("deleteImages");
                if (deleteImageIds != null)
                {
                    foreach (var imgIdStr in deleteImageIds)
                    {
                        if (int.TryParse(imgIdStr, out int imgId))
                        {
                            var img = product.Product_Images.FirstOrDefault(i => i.Id == imgId);
                            if (img != null)
                                db.product_images.Remove(img);
                        }
                    }
                }

                // Cập nhật ảnh đại diện
                string mainImageIdStr = Request["mainImageId"];
                if (int.TryParse(mainImageIdStr, out int mainImageId))
                {
                    foreach (var img in product.Product_Images)
                    {
                        img.Is_Main = (img.Id == mainImageId);
                    }
                }

                // Xử lý ảnh mới upload
                var files = Request.Files;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        string path = Path.Combine(Server.MapPath("~/Uploads/Products"), fileName);
                        file.SaveAs(path);

                        product.Product_Images.Add(new Product_Image
                        {
                            Image_Url = "/Uploads/Products/" + fileName,
                            Is_Main = false,
                            Product_Id = product.Id
                        });
                    }
                }

                // Xóa SKU cũ
                db.product_skus.RemoveRange(product.Product_Sku);

                // Thêm SKU mới
                var skuKeys = Request.Form.AllKeys.Where(k => k.StartsWith("skus[") && k.EndsWith("].Sku")).ToList();
                foreach (var key in skuKeys)
                {
                    var prefix = key.Substring(0, key.IndexOf("].Sku") + 1); // skus[0], skus[1], ...
                    string sku = Request[prefix + ".Sku"];
                    string color = Request[prefix + ".Color"];
                    string size = Request[prefix + ".Size"];
                    int quantity = int.TryParse(Request[prefix + ".Quantity"], out int q) ? q : 0;
                    decimal price = decimal.TryParse(Request[prefix + ".Price"], out decimal p) ? p : 0;

                    product.Product_Sku.Add(new Product_Sku
                    {
                        Sku = sku,
                        Color = color,
                        Size = size,
                        Quantity = quantity,
                        Price = price,
                        Product_Id = product.Id
                    });
                }

                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();

                return Json(new { success = true, redirectUrl = Url.Action("Index") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu: " + ex.Message });
            }
        }


        // POST: Delete Product (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteAjax(int id)
        {
            var product = db.products
                .Include(p => p.Product_Images)
                .Include(p => p.Product_Sku)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            // Xóa ảnh vật lý
            if (product.Product_Images != null)
            {
                foreach (var img in product.Product_Images.ToList())
                {
                    try
                    {
                        var path = Server.MapPath(img.Image_Url);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                    }
                    catch { }
                }
            }

            db.product_images.RemoveRange(product.Product_Images ?? new List<Product_Image>());
            db.product_skus.RemoveRange(product.Product_Sku ?? new List<Product_Sku>());
            db.products.Remove(product);
            db.SaveChanges();

            return Json(new { success = true, message = "Xóa thành công" });
        }

        // GET: Trạng thái sản phẩm
        [HttpGet]
        public JsonResult GetStatusList()
        {
            var statuses = new[] { "Đang bán", "Chưa bán", "Đã hết" };
            return Json(statuses, JsonRequestBehavior.AllowGet);
        }

        // GET: Danh sách category
        [HttpGet]
        public JsonResult GetCategories()
        {
            var categories = db.categories
                .Select(c => new { c.Id, c.Name })
                .ToList();
            return Json(categories, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
