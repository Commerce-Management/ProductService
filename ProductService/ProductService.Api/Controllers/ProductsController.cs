using Microsoft.AspNetCore.Mvc;

namespace ProductService.Controllers;

public class ProductsController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}