using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookstore.Web.Helpers
{
    public static class ControllerExtensions
    {
        public static void SetNotification(this Controller controller, string message)
        {
            controller.TempData["Notification"] = message;
        }
    }
}