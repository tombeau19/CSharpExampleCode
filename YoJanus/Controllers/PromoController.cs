using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YoJanus.Web.Auth;
using YoJanus.Web.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Collections;
using System.Net.Mail;

namespace YoJanus.Web.Controllers
{
    public class PromoController : Controller
    {
        private readonly SurveyTestContext _context;

        public PromoController(SurveyTestContext context)
        {
            _context = context;
        }

        [YoAuthorize]        
        public IActionResult Index()
        {
            return View();
        }

        [YoAuthorize]
        public ActionResult ViewUsers(string sortOrder, string searchString)
        {
            ViewBag.GroupNameSortParm = String.IsNullOrEmpty(sortOrder) ? "groupName_desc" : "";
            ViewBag.CodeSortParm = sortOrder == "Code" ? "code_desc" : "Code";
            ViewBag.FirstNameSortParm = sortOrder == "FirstName" ? "firstName_desc": "FirstName";
            ViewBag.LastNameSortParm = sortOrder == "LastName" ? "lastName_desc": "LastName";
            ViewBag.EmailSortParm = sortOrder == "Email" ? "email_desc": "Email";
            var users = from u in _context.Promocode_User_View select u;
            if (!String.IsNullOrEmpty(searchString))
            {
                users = users.Where(u => u.Code.Contains(searchString) || u.GroupName.Contains(searchString) || u.FirstName.Contains(searchString) || u.LastName.Contains(searchString) || u.Email.Contains(searchString));
            }
            switch (sortOrder)
            {
                case "groupName_desc":
                    users = users.OrderByDescending(u => u.GroupName);
                    break;
                case "Code":
                    users = users.OrderBy(u => u.Code);
                    break;
                case "code_desc":
                    users = users.OrderByDescending(u=>u.Code);
                    break;
                case "FirstName":
                    users = users.OrderBy(u => u.FirstName);
                    break;
                case "firstName_desc":
                    users = users.OrderByDescending(u => u.FirstName);
                    break;
                case "LastName":
                    users = users.OrderBy(u => u.LastName);
                    break;
                case "lastName_desc":
                    users = users.OrderByDescending(u => u.LastName);
                    break;
                case "Email":
                    users = users.OrderBy(u => u.Email);
                    break;
                case "email_desc":
                    users = users.OrderByDescending(u => u.Email);
                    break;
                default:
                    users = users.OrderBy(u=>u.GroupName);
                    break;
            }
            return View(users.ToList());
        }

        [YoAuthorize]
        public async Task<IActionResult> ViewCodes()
        {
            return View(await _context.PromoCodes.ToListAsync());
        }

        [YoAuthorize]
        public IActionResult AddPromoCode()
        {
            return View();
        }

        public ActionResult IsCodeUnique(string code){
            var usedCode = _context.PromoCodes.FirstOrDefault(s=>s.Code == code);
            if(usedCode != null){
                return Json("Code already in use");
            } 
            return Json(true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePromoCode(PromoCodes promoCode)
        {
            if(promoCode.EndDate.Value < promoCode.StartDate.Value || promoCode.EndDate.Value == promoCode.StartDate.Value){
                return RedirectToAction("PromoCodeFailure");
            } else {
            _context.PromoCodes.Add(promoCode);
            await _context.SaveChangesAsync();
            return RedirectToAction("PromoCodeAdded");
            }
        }

        [YoAuthorize]
        public IActionResult PromoCodeAdded()
        {
            return View();
        }

        [YoAuthorize]
        public IActionResult PromoCodeFailure()
        {
            return View();
        }

        public ActionResult ValidatePromoCode(string code){
            var usedCode = _context.PromoCodeUsers.FirstOrDefault(s=>s.Code == code && s.UserID == this.User.UserId());
            var validCode = _context.PromoCodes.FirstOrDefault(s=>s.Code == code);
            if(validCode == null){
                return Json("Please Enter a Valid Promo Code");
            } else if(validCode.StartDate > DateTime.Now){
                return Json("Promotional Period has not started yet");
            } else if(validCode.EndDate.Value.AddDays(1) < DateTime.Now){
                return Json("Promotional Period has ended");
            } else if(usedCode != null){
                return Json("You have already used this Promo Code");
            }
            return Json(true);
        }

        public ActionResult ValidateEmail(string email){
            try{
                MailAddress m = new MailAddress(email);
                return Json(true);
            } catch (FormatException) {
                return Json("Please enter a valid Email");
            }
        }

        public IActionResult ThankYou(){
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterPromoCode(PromoCodeUsers promoCodeUser)
        {
            var userInfo = _context.Users.Find(this.User.UserId());
            promoCodeUser.UserID = userInfo.Id;
            promoCodeUser.FirstName = userInfo.FirstName;
            promoCodeUser.LastName = userInfo.LastName;
            _context.PromoCodeUsers.Add(promoCodeUser);
            await _context.SaveChangesAsync();
            return RedirectToAction("ThankYou");
        }

        [HttpPostAttribute]
        public async Task<HttpResponseMessage> DeletePromoCode(Guid id) {
            PromoCodes promoCode = _context.PromoCodes.Find(id);
            if (promoCode != null) {
                _context.PromoCodes.Remove(promoCode);
            }
            int changes = await _context.SaveChangesAsync();
            var response = new HttpResponseMessage();
            if (changes > 0) {
                response.StatusCode = HttpStatusCode.OK;
                response.ReasonPhrase = "OK";
            }
            else {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.ReasonPhrase = "Bad Request";
            }
            return response;
        }
    }
}