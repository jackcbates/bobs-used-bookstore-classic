using Microsoft.AspNetCore.Authorization;
﻿
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookstore.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrators")]
    public abstract class AdminAreaControllerBase : Controller { }
}