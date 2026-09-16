// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<DatPhongKhachSanUser> _userManager;
        private readonly SignInManager<DatPhongKhachSanUser> _signInManager;
        private readonly DatPhongKhachSanContext _db;

        public IndexModel(
            UserManager<DatPhongKhachSanUser> userManager,
            SignInManager<DatPhongKhachSanUser> signInManager,
            DatPhongKhachSanContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
            [StringLength(150)]
            [Display(Name = "Họ và tên")]
            public string HoTen { get; set; }

            [StringLength(20)]
            [Display(Name = "Số điện thoại")]
            public string SoDienThoai { get; set; }

            [StringLength(20)]
            [Display(Name = "CCCD / Hộ chiếu")]
            public string CCCD { get; set; }
        }

        private async Task LoadAsync(DatPhongKhachSanUser user)
        {
            Username = await _userManager.GetUserNameAsync(user);

            var nguoiDung = await _db.NguoiDungs
                .FirstOrDefaultAsync(nd => nd.UserId == user.Id);

            Input = new InputModel
            {
                HoTen       = nguoiDung?.HoTen ?? string.Empty,
                SoDienThoai = nguoiDung?.SoDienThoai ?? string.Empty,
                CCCD        = nguoiDung?.CCCD ?? string.Empty,
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var nguoiDung = await _db.NguoiDungs
                .FirstOrDefaultAsync(nd => nd.UserId == user.Id);

            if (nguoiDung != null)
            {
                nguoiDung.HoTen       = Input.HoTen;
                nguoiDung.SoDienThoai = Input.SoDienThoai;
                nguoiDung.CCCD        = Input.CCCD;
                await _db.SaveChangesAsync();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ đã được cập nhật.";
            return RedirectToPage();
        }
    }
}
