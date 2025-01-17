﻿using Microsoft.AspNet.Mvc;
using System;
using System.Net;
using WebVella.ERP.Api.Models;
using WebVella.ERP.Web.Security;

namespace WebVella.ERP.Web.Controllers
{
    [AuthorizeAttribute]
    public class ApiControllerBase : Controller
    {
        protected IErpService service;

        public ApiControllerBase(IErpService service)
        {
            this.service = service;
        }

        public IActionResult DoResponse( BaseResponseModel response )
        {
            if (response.Errors.Count > 0 || !response.Success)
            {
                if( response.StatusCode == HttpStatusCode.OK )
                    Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                else
                    Context.Response.StatusCode = (int)response.StatusCode;
            }

            return Json(response);
        }

        public IActionResult DoPageNotFoundResponse()
        {
            Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return Json(new { });
        }

        public IActionResult DoBadRequestResponse(BaseResponseModel response, string message = null, Exception ex = null)
        {
            response.Timestamp = DateTime.UtcNow;
            response.Success = false;
#if DEBUG
            if (ex != null)
            {
                response.Message = ex.Message + ex.StackTrace;
            }
#else
            if (string.IsNullOrEmpty( message ))
                response.Message = "An internal error occurred!";
#endif
            Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return Json(response);
        }
    }
}
