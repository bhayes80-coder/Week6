using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Bookstore.Models;

namespace Bookstore.Controllers
{
    public class BookController : Controller
    {
        private Repository<Book> data { get; set; }
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public BookController(BookstoreContext ctx, IWebHostEnvironment env, IConfiguration config)
        {
            data = new Repository<Book>(ctx);
            _env = env;
            _config = config;
        }

        public RedirectToActionResult Index() => RedirectToAction("List");

        public ViewResult List(BookGridData values)
        {
            // create options for querying books
            var options = new QueryOptions<Book> { 
                Includes = "Authors, Genre",
                OrderByDirection = values.SortDirection,
                PageNumber = values.PageNumber,
                PageSize = values.PageSize
            };
            if (values.IsSortByGenre) 
                options.OrderBy = b => b.GenreId;
            else if (values.IsSortByPrice) 
                options.OrderBy = b => b.Price;
            else 
                options.OrderBy = b => b.Title;

            // create view model
            var vm = new BookListViewModel { 
                Books = data.List(options),
                CurrentRoute = values,
                TotalPages = values.GetTotalPages(data.Count)
            };

            return View(vm);
        }

        public ViewResult Details(int id)
        {
            var book = data.Get(new QueryOptions<Book> {
                Where = b => b.BookId == id,
                Includes = "Authors, Genre"
            }) ?? new Book();
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public RedirectToActionResult ExportToXml()
        {
            // 1. Query ALL books (no PageNumber/PageSize, so no paging is applied)
            var options = new QueryOptions<Book> {
                Includes = "Authors, Genre",
                OrderBy = b => b.Title
            };
            var books = data.List(options).ToList();

            // 2. Build the XML document with LINQ to XML
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("Books",
                    books.Select(book => new XElement("Book",
                        new XAttribute("Id", book.BookId),
                        new XElement("Title", book.Title),
                        new XElement("Authors",
                            book.Authors.Select(author =>
                                new XElement("Author", author.FullName))),
                        new XElement("Price",
                            book.Price.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement("Genre", book.Genre?.Name ?? string.Empty)
                    ))
                )
            );

            // 3. Save the file. Folder and file name come from appsettings.json (XmlExport section)
            string folder = _config["XmlExport:FolderPath"] ?? "wwwroot/data";
            string fileName = _config["XmlExport:FileName"] ?? "books.xml";
            string fullFolder = Path.Combine(_env.ContentRootPath, folder);

            Directory.CreateDirectory(fullFolder);   // does nothing if it already exists
            doc.Save(Path.Combine(fullFolder, fileName));

            // 4. Confirmation message (displayed by _Layout.cshtml) and redirect back to the list
            TempData["message"] = $"Exported {books.Count} books to {folder}/{fileName}.";
            return RedirectToAction("List");
        }

        [HttpPost]
        public RedirectToActionResult PageSize(BookGridData currentRoute)
        {
            return RedirectToAction("List", currentRoute.ToDictionary());
        }
    }   
}