using System.Security.Claims;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<DatPhongKhachSanUser>>();
        var db          = serviceProvider.GetRequiredService<DatPhongKhachSanContext>();
        var config      = serviceProvider.GetRequiredService<IConfiguration>();

        // ── 0. Fix TrangThai cho dữ liệu cũ ─────────────────────────────────────
        await db.LoaiPhongs.Where(l => !l.TrangThai)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.TrangThai, true));
        await db.DichVus.Where(d => !d.TrangThai)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.TrangThai, true));

        // ── 1. Roles ──────────────────────────────────────────────────────────────
        string[] roles = ["Admin", "NhanVien", "KhachHang"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── 1b. Gán KhachHang cho user chưa có role; tạo NguoiDung nếu thiếu ────
        foreach (var u in userManager.Users.ToList())
        {
            var userRoles = await userManager.GetRolesAsync(u);
            if (userRoles.Count == 0)
                await userManager.AddToRoleAsync(u, "KhachHang");

            if (!await db.NguoiDungs.AnyAsync(n => n.UserId == u.Id))
            {
                var r = (await userManager.GetRolesAsync(u)).FirstOrDefault() ?? "KhachHang";
                var vaiTro = r == "Admin" ? "Admin" : r == "NhanVien" ? "NhanVien" : "KhachHang";
                db.NguoiDungs.Add(new NguoiDung
                {
                    UserId      = u.Id,
                    HoTen       = u.Email ?? u.UserName ?? "Người dùng",
                    Email       = u.Email,
                    VaiTro      = vaiTro,
                    PhanLoai    = "Thuong"
                });
            }
        }
        await db.SaveChangesAsync();

        // ── 2. Tài khoản Admin ────────────────────────────────────────────────────
        // DEPLOY: PHẢI set env var SeedAdmin__Password trên host, nếu không
        // admin dùng mật khẩu fallback công khai "Admin@123" — KHÔNG an toàn trên production.
        var adminPass = config["SeedAdmin:Password"] ?? "Admin@123";
        var adminUser = await SeedUserAsync(userManager, db, "admin@hotel.vn", adminPass, "Quản trị viên", "Admin");
        // admin@hotel.vn = admin tối cao => gán tất cả claim (plan 8B)
        if (adminUser != null)
        {
            var existingClaims = await userManager.GetClaimsAsync(adminUser);
            foreach (var cn in ChucNang.TatCa)
            {
                if (!existingClaims.Any(c => c.Type == ChucNang.ClaimType && c.Value == cn))
                    await userManager.AddClaimAsync(adminUser,
                        new System.Security.Claims.Claim(ChucNang.ClaimType, cn));
            }
        }

        // ── 2b. Tài khoản Demo (công bố README) — quyền XEM, không sửa/xóa ────────
        var demoUser = await SeedUserAsync(userManager, db, "demo@hotel.vn", "Demo@123", "Tài khoản Demo", "NhanVien");
        if (demoUser != null)
        {
            var existingClaims = await userManager.GetClaimsAsync(demoUser);
            // Chỉ cấp quyền đọc: tra cứu đơn + xem báo cáo
            string[] demoClaims = [ChucNang.TraCuuDon, ChucNang.XemBaoCao];
            foreach (var cn in demoClaims)
            {
                if (!existingClaims.Any(c => c.Type == ChucNang.ClaimType && c.Value == cn))
                    await userManager.AddClaimAsync(demoUser,
                        new System.Security.Claims.Claim(ChucNang.ClaimType, cn));
            }
        }

        // ── 3. Lễ tân ─────────────────────────────────────────────────────────────
        await SeedUserAsync(userManager, db, "letan@hotel.vn",  "Letan@123",  "Nguyễn Lễ Tân",  "NhanVien");
        await SeedUserAsync(userManager, db, "letan2@hotel.vn", "Letan2@123", "Trần Lễ Tân 2",  "NhanVien");

        // ── 4. Khách hàng demo ────────────────────────────────────────────────────
        await SeedUserAsync(userManager, db, "user1@test.com",  "User1@123",  "Nguyễn Test Một", "KhachHang");
        await SeedUserAsync(userManager, db, "user2@test.com",  "User2@123",  "Lê Test Hai",     "KhachHang");
        await SeedUserAsync(userManager, db, "khach@gmail.com", "Khach@123",  "Trần Văn Khách",  "KhachHang",
            soDienThoai: "0901234567");

        // ── 5. Khách walk-in demo (UserId = NULL) ─────────────────────────────────
        if (!await db.NguoiDungs.AnyAsync(n => n.UserId == null && n.HoTen == "Phạm Walk-In"))
        {
            db.NguoiDungs.Add(new NguoiDung
            {
                UserId      = null,
                HoTen       = "Phạm Walk-In",
                SoDienThoai = "0912345678",
                CCCD        = "001234567890",
                VaiTro      = "KhachHang",
                PhanLoai    = "Thuong"
            });
            await db.SaveChangesAsync();
        }

        // ── 6. Tiện nghi ──────────────────────────────────────────────────────────
        if (!await db.TienNghis.AnyAsync())
        {
            db.TienNghis.AddRange(
                new TienNghi { TenTienNghi = "WiFi miễn phí",     BieuTuong = "fa-wifi" },
                new TienNghi { TenTienNghi = "Điều hòa",          BieuTuong = "fa-snowflake" },
                new TienNghi { TenTienNghi = "TV màn hình phẳng",  BieuTuong = "fa-tv" },
                new TienNghi { TenTienNghi = "Minibar",            BieuTuong = "fa-wine-glass" },
                new TienNghi { TenTienNghi = "Bồn tắm",           BieuTuong = "fa-bath" },
                new TienNghi { TenTienNghi = "Két an toàn",        BieuTuong = "fa-lock" },
                new TienNghi { TenTienNghi = "Ban công",           BieuTuong = "fa-door-open" },
                new TienNghi { TenTienNghi = "View biển",          BieuTuong = "fa-umbrella-beach" }
            );
            await db.SaveChangesAsync();
        }

        // ── 7. Loại phòng ─────────────────────────────────────────────────────────
        if (!await db.LoaiPhongs.AnyAsync())
        {
            var tienNghis = await db.TienNghis.ToListAsync();
            var wifi = tienNghis.First(t => t.TenTienNghi == "WiFi miễn phí");
            var ac   = tienNghis.First(t => t.TenTienNghi == "Điều hòa");
            var tv   = tienNghis.First(t => t.TenTienNghi == "TV màn hình phẳng");
            var mini = tienNghis.First(t => t.TenTienNghi == "Minibar");
            var bath = tienNghis.First(t => t.TenTienNghi == "Bồn tắm");
            var safe = tienNghis.First(t => t.TenTienNghi == "Két an toàn");
            var balc = tienNghis.First(t => t.TenTienNghi == "Ban công");
            var sea  = tienNghis.First(t => t.TenTienNghi == "View biển");

            db.LoaiPhongs.AddRange(
                new LoaiPhong { TenLoaiPhong = "Phòng Standard", MoTa = "Phòng tiêu chuẩn thoải mái.", GiaCoBan = 800_000,   SucChuaNguoiLon = 2, SucChuaTreEm = 1, LoaiGiuong = "Giường đôi",               DienTich = 25, MaTienNghis = [wifi, ac, tv] },
                new LoaiPhong { TenLoaiPhong = "Phòng Deluxe",   MoTa = "Phòng cao cấp, ban công.",    GiaCoBan = 1_200_000, SucChuaNguoiLon = 2, SucChuaTreEm = 2, LoaiGiuong = "Giường King",               DienTich = 35, MaTienNghis = [wifi, ac, tv, mini, balc] },
                new LoaiPhong { TenLoaiPhong = "Phòng Superior", MoTa = "View biển tuyệt đẹp.",        GiaCoBan = 1_800_000, SucChuaNguoiLon = 3, SucChuaTreEm = 2, LoaiGiuong = "Giường King + Giường phụ", DienTich = 45, MaTienNghis = [wifi, ac, tv, mini, safe, sea] },
                new LoaiPhong { TenLoaiPhong = "Phòng Suite",    MoTa = "Suite sang trọng, view biển.", GiaCoBan = 3_500_000, SucChuaNguoiLon = 4, SucChuaTreEm = 2, LoaiGiuong = "Giường King",               DienTich = 80, MaTienNghis = [wifi, ac, tv, mini, bath, safe, balc, sea] }
            );
            await db.SaveChangesAsync();
        }

        // ── 8. Phòng ──────────────────────────────────────────────────────────────
        if (!await db.Phongs.AnyAsync())
        {
            var lps = await db.LoaiPhongs.ToListAsync();
            var std  = lps.First(l => l.TenLoaiPhong == "Phòng Standard");
            var dlx  = lps.First(l => l.TenLoaiPhong == "Phòng Deluxe");
            var sup  = lps.First(l => l.TenLoaiPhong == "Phòng Superior");
            var suit = lps.First(l => l.TenLoaiPhong == "Phòng Suite");
            db.Phongs.AddRange(
                new Phong { SoPhong = "101", Tang = 1, MaLoaiPhong = std.MaLoaiPhong },
                new Phong { SoPhong = "102", Tang = 1, MaLoaiPhong = std.MaLoaiPhong },
                new Phong { SoPhong = "103", Tang = 1, MaLoaiPhong = std.MaLoaiPhong },
                new Phong { SoPhong = "201", Tang = 2, MaLoaiPhong = dlx.MaLoaiPhong },
                new Phong { SoPhong = "202", Tang = 2, MaLoaiPhong = dlx.MaLoaiPhong },
                new Phong { SoPhong = "301", Tang = 3, MaLoaiPhong = sup.MaLoaiPhong },
                new Phong { SoPhong = "302", Tang = 3, MaLoaiPhong = sup.MaLoaiPhong },
                new Phong { SoPhong = "401", Tang = 4, MaLoaiPhong = suit.MaLoaiPhong },
                new Phong { SoPhong = "402", Tang = 4, MaLoaiPhong = suit.MaLoaiPhong },
                new Phong { SoPhong = "501", Tang = 5, MaLoaiPhong = suit.MaLoaiPhong }
            );
            await db.SaveChangesAsync();
        }

        // ── 8b. Ảnh loại phòng ────────────────────────────────────────────────────
        if (!await db.AnhPhongs.AnyAsync())
        {
            var lps = await db.LoaiPhongs.ToListAsync();
            var slugs = new Dictionary<string, string>
            {
                ["Phòng Standard"] = "standard",
                ["Phòng Deluxe"]   = "deluxe",
                ["Phòng Superior"] = "superior",
                ["Phòng Suite"]    = "suite",
            };
            foreach (var lp in lps)
            {
                if (!slugs.TryGetValue(lp.TenLoaiPhong, out var slug)) continue;
                db.AnhPhongs.AddRange(
                    new AnhPhong { MaLoaiPhong = lp.MaLoaiPhong, DuongDanAnh = $"/images/phong/{slug}-1.jpg", LaAnhChinh = true,  ThuTu = 1 },
                    new AnhPhong { MaLoaiPhong = lp.MaLoaiPhong, DuongDanAnh = $"/images/phong/{slug}-2.jpg", LaAnhChinh = false, ThuTu = 2 },
                    new AnhPhong { MaLoaiPhong = lp.MaLoaiPhong, DuongDanAnh = $"/images/phong/{slug}-3.jpg", LaAnhChinh = false, ThuTu = 3 }
                );
            }
            await db.SaveChangesAsync();
        }

        // ── 9. Dịch vụ ────────────────────────────────────────────────────────────
        if (!await db.DichVus.AnyAsync())
        {
            db.DichVus.AddRange(
                new DichVu { TenDichVu = "Bữa sáng",         Gia = 120_000, DonVi = "người/ngày" },
                new DichVu { TenDichVu = "Đưa đón sân bay",  Gia = 350_000, DonVi = "lượt" },
                new DichVu { TenDichVu = "Giặt ủi",          Gia =  80_000, DonVi = "kg" },
                new DichVu { TenDichVu = "Spa & Massage",     Gia = 500_000, DonVi = "lượt" },
                new DichVu { TenDichVu = "Thuê xe máy",      Gia = 150_000, DonVi = "ngày" }
            );
            await db.SaveChangesAsync();
        }

        // ── 10. Ca làm việc ───────────────────────────────────────────────────────
        if (!await db.CaLamViecs.AnyAsync())
        {
            db.CaLamViecs.AddRange(
                new CaLamViec { TenCa = "Ca sáng",  GioBatDau = new TimeOnly(6,  0), GioKetThuc = new TimeOnly(14, 0) },
                new CaLamViec { TenCa = "Ca chiều", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0) },
                new CaLamViec { TenCa = "Ca tối",   GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6,  0) }
            );
            await db.SaveChangesAsync();
        }

        // ── 11. Demo nhân viên bổ sung ────────────────────────────────────────
        await SeedUserAsync(userManager, db, "buongphong@hotel.vn", "Buong@123", "Lê Buồng Phòng", "NhanVien");

        // ── 12. Demo đơn đặt phòng (Phase 16) ────────────────────────────────
        if (!await db.DatPhongs.AnyAsync(d => d.MaDon.StartsWith("DEMO")))
        {
            var phongs    = await db.Phongs.ToDictionaryAsync(p => p.SoPhong);
            var lps       = await db.LoaiPhongs.ToDictionaryAsync(l => l.TenLoaiPhong);
            var dvBuaSang = await db.DichVus.FirstOrDefaultAsync(d => d.TenDichVu == "Bữa sáng");
            var ndKhach   = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "khach@gmail.com");
            var ndUser1   = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "user1@test.com");
            var ndUser2   = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "user2@test.com");
            var ndWalkIn  = await db.NguoiDungs.FirstOrDefaultAsync(n => n.HoTen == "Phạm Walk-In");

            if (ndKhach != null && ndWalkIn != null && phongs.ContainsKey("101"))
            {
                var today  = DateOnly.FromDateTime(DateTime.Today);
                var now    = DateTime.Now;
                var giaStd = lps.TryGetValue("Phòng Standard", out var lpStd) ? lpStd.GiaCoBan : 800_000m;
                var giaDlx = lps.TryGetValue("Phòng Deluxe",   out var lpDlx) ? lpDlx.GiaCoBan : 1_200_000m;
                var giaSup = lps.TryGetValue("Phòng Superior", out var lpSup) ? lpSup.GiaCoBan : 1_800_000m;

                // anonymous NguoiDung cho DEMO007 (đặt nhanh không tài khoản)
                var ndAnon = new NguoiDung { UserId = null, HoTen = "Hoàng Đặt Nhanh",
                    SoDienThoai = "0988123456", VaiTro = "KhachHang" };
                db.NguoiDungs.Add(ndAnon);
                await db.SaveChangesAsync();

                // ── DEMO001: DaTraPhong - Online - Phòng 101 - 33=>30 ngày trước ──
                {
                    var don = new DatPhong { MaNguoiDung = ndKhach.MaNguoiDung, MaDon = "DEMO001",
                        NguonDat = "Online", NgayNhanPhong = today.AddDays(-33), NgayTraPhong = today.AddDays(-30),
                        SoDem = 3, TongSoNguoiLon = 2, TongSoTreEm = 0,
                        LoaiThanhToan = "ThanhToanDu", TrangThai = "DaTraPhong", NgayDat = now.AddDays(-35) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["101"].MaPhong, SoNguoiLon = 2, SoTreEm = 0,
                        GiaMotDem = giaStd, TenKhachLuuTru = ndKhach.HoTen });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = giaStd * 3,
                        PhuongThuc = "VNPay", LoaiThanhToan = "ThanhToanDu", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-35), NgayTao = now.AddDays(-35) });
                    await db.SaveChangesAsync();
                }

                // ── DEMO002: DaTraPhong - WalkIn - Phòng 201 - 24=>20 ngày trước ──
                if (phongs.ContainsKey("201"))
                {
                    var don = new DatPhong { MaNguoiDung = ndWalkIn.MaNguoiDung, MaDon = "DEMO002",
                        NguonDat = "WalkIn", NgayNhanPhong = today.AddDays(-24), NgayTraPhong = today.AddDays(-20),
                        SoDem = 4, TongSoNguoiLon = 2, TongSoTreEm = 1,
                        LoaiThanhToan = "ThanhToanDu", TrangThai = "DaTraPhong", NgayDat = now.AddDays(-25) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["201"].MaPhong, SoNguoiLon = 2, SoTreEm = 1,
                        GiaMotDem = giaDlx, TenKhachLuuTru = ndWalkIn.HoTen });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = giaDlx * 4,
                        PhuongThuc = "TienMat", LoaiThanhToan = "ThanhToanDu", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-25), NgayTao = now.AddDays(-25) });
                    await db.SaveChangesAsync();
                }

                // ── DEMO003: DaTraPhong - Online - Phòng 301 + Bữa sáng - 14=>11 ngày trước ──
                if (ndUser1 != null && phongs.ContainsKey("301"))
                {
                    var don = new DatPhong { MaNguoiDung = ndUser1.MaNguoiDung, MaDon = "DEMO003",
                        NguonDat = "Online", NgayNhanPhong = today.AddDays(-14), NgayTraPhong = today.AddDays(-11),
                        SoDem = 3, TongSoNguoiLon = 2, TongSoTreEm = 0,
                        LoaiThanhToan = "Coc30", TrangThai = "DaTraPhong", NgayDat = now.AddDays(-16) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    var ct3 = new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["301"].MaPhong, SoNguoiLon = 2, SoTreEm = 0,
                        GiaMotDem = giaSup, TenKhachLuuTru = ndUser1.HoTen };
                    db.ChiTietDatPhongs.Add(ct3); await db.SaveChangesAsync();
                    if (dvBuaSang != null)
                        db.ChiTietDichVus.Add(new ChiTietDichVu { MaChiTiet = ct3.MaChiTiet,
                            MaDichVu = dvBuaSang.MaDichVu, SoLuong = 4, DonGia = dvBuaSang.Gia,
                            ThanhTien = dvBuaSang.Gia * 4 });
                    decimal tong3 = giaSup * 3 + (dvBuaSang != null ? dvBuaSang.Gia * 4 : 0);
                    decimal coc3  = Math.Round(tong3 * 0.3m);
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = coc3,
                        PhuongThuc = "VNPay", LoaiThanhToan = "DatCoc", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-16), NgayTao = now.AddDays(-16) });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = tong3 - coc3,
                        PhuongThuc = "TienMat", LoaiThanhToan = "ThanhToanDu", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-14), NgayTao = now.AddDays(-14) });
                    await db.SaveChangesAsync();
                }

                // ── DEMO004: DaTraPhong - Online - Phòng 102 - 3=>1 ngày trước => DangDonDep ──
                if (phongs.ContainsKey("102"))
                {
                    var don = new DatPhong { MaNguoiDung = ndKhach.MaNguoiDung, MaDon = "DEMO004",
                        NguonDat = "Online", NgayNhanPhong = today.AddDays(-3), NgayTraPhong = today.AddDays(-1),
                        SoDem = 2, TongSoNguoiLon = 2, TongSoTreEm = 0,
                        LoaiThanhToan = "ThanhToanDu", TrangThai = "DaTraPhong", NgayDat = now.AddDays(-4) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["102"].MaPhong, SoNguoiLon = 2, SoTreEm = 0,
                        GiaMotDem = giaStd, TenKhachLuuTru = ndKhach.HoTen });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = giaStd * 2,
                        PhuongThuc = "VNPay", LoaiThanhToan = "ThanhToanDu", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-4), NgayTao = now.AddDays(-4) });
                    await db.SaveChangesAsync();
                    phongs["102"].TrangThai = "DangDonDep";
                    await db.SaveChangesAsync();
                }

                // ── DEMO005: DangO - WalkIn - Phòng 202 - hôm qua => +3 ngày tới ──
                if (phongs.ContainsKey("202"))
                {
                    var don = new DatPhong { MaNguoiDung = ndWalkIn.MaNguoiDung, MaDon = "DEMO005",
                        NguonDat = "WalkIn", NgayNhanPhong = today.AddDays(-1), NgayTraPhong = today.AddDays(3),
                        SoDem = 4, TongSoNguoiLon = 2, TongSoTreEm = 0,
                        LoaiThanhToan = "ThanhToanDu", TrangThai = "DangO", NgayDat = now.AddDays(-1) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["202"].MaPhong, SoNguoiLon = 2, SoTreEm = 0,
                        GiaMotDem = giaDlx, TenKhachLuuTru = ndWalkIn.HoTen });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = giaDlx * 4,
                        PhuongThuc = "TienMat", LoaiThanhToan = "ThanhToanDu", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-1), NgayTao = now.AddDays(-1) });
                    await db.SaveChangesAsync();
                    phongs["202"].TrangThai = "DangO";
                    await db.SaveChangesAsync();
                }

                // ── DEMO006: DaXacNhan - Online - Phòng 302 - +3=>+6 ngày tới (cọc 30%) ──
                if (ndUser2 != null && phongs.ContainsKey("302"))
                {
                    var don = new DatPhong { MaNguoiDung = ndUser2.MaNguoiDung, MaDon = "DEMO006",
                        NguonDat = "Online", NgayNhanPhong = today.AddDays(3), NgayTraPhong = today.AddDays(6),
                        SoDem = 3, TongSoNguoiLon = 3, TongSoTreEm = 1,
                        LoaiThanhToan = "Coc30", TrangThai = "DaXacNhan", NgayDat = now.AddDays(-1) };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["302"].MaPhong, SoNguoiLon = 3, SoTreEm = 1,
                        GiaMotDem = giaSup, TenKhachLuuTru = ndUser2.HoTen });
                    decimal coc6 = Math.Round(giaSup * 3 * 0.3m);
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = coc6,
                        PhuongThuc = "VNPay", LoaiThanhToan = "DatCoc", TrangThai = "ThanhCong",
                        NgayThanhToan = now.AddDays(-1), NgayTao = now.AddDays(-1) });
                    await db.SaveChangesAsync();
                }

                // ── DEMO007: ChoXacNhan - Online - Phòng 103 - +7=>+9 ngày tới ──
                if (phongs.ContainsKey("103"))
                {
                    var don = new DatPhong { MaNguoiDung = ndAnon.MaNguoiDung, MaDon = "DEMO007",
                        NguonDat = "Online", NgayNhanPhong = today.AddDays(7), NgayTraPhong = today.AddDays(9),
                        SoDem = 2, TongSoNguoiLon = 1, TongSoTreEm = 0,
                        LoaiThanhToan = "ThanhToanDu", TrangThai = "ChoXacNhan", NgayDat = now };
                    db.DatPhongs.Add(don); await db.SaveChangesAsync();
                    db.ChiTietDatPhongs.Add(new ChiTietDatPhong { MaDatPhong = don.MaDatPhong,
                        MaPhong = phongs["103"].MaPhong, SoNguoiLon = 1, SoTreEm = 0,
                        GiaMotDem = giaStd, TenKhachLuuTru = ndAnon.HoTen });
                    db.ThanhToans.Add(new ThanhToan { MaDatPhong = don.MaDatPhong, SoTien = giaStd * 2,
                        PhuongThuc = "VNPay", LoaiThanhToan = "ThanhToanDu", TrangThai = "ChoXuLy",
                        NgayTao = now });
                    await db.SaveChangesAsync();
                }
            }
        }

        // ── 13. Demo phân ca (7 ngày qua + hôm nay + 7 ngày tới) ────────────
        if (!await db.PhanCas.AnyAsync())
        {
            var ndLeTan  = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "letan@hotel.vn");
            var ndLeTan2 = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "letan2@hotel.vn");
            var caSang   = await db.CaLamViecs.FirstOrDefaultAsync(c => c.TenCa == "Ca sáng");
            var caChieu  = await db.CaLamViecs.FirstOrDefaultAsync(c => c.TenCa == "Ca chiều");

            if (ndLeTan != null && ndLeTan2 != null && caSang != null && caChieu != null)
            {
                for (int i = -7; i <= 7; i++)
                {
                    var ngay = DateOnly.FromDateTime(DateTime.Today.AddDays(i));
                    db.PhanCas.Add(new PhanCa { MaNguoiDung = ndLeTan.MaNguoiDung,  NgayLam = ngay, MaCa = caSang.MaCa });
                    db.PhanCas.Add(new PhanCa { MaNguoiDung = ndLeTan2.MaNguoiDung, NgayLam = ngay, MaCa = caChieu.MaCa });
                }
                await db.SaveChangesAsync();
            }
        }

        // ── 14. Demo chấm công (7 ngày qua) ──────────────────────────────────
        if (!await db.ChamCongs.AnyAsync())
        {
            var ndLeTan  = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "letan@hotel.vn");
            var ndLeTan2 = await db.NguoiDungs.FirstOrDefaultAsync(n => n.Email == "letan2@hotel.vn");
            var caSang   = await db.CaLamViecs.FirstOrDefaultAsync(c => c.TenCa == "Ca sáng");
            var caChieu  = await db.CaLamViecs.FirstOrDefaultAsync(c => c.TenCa == "Ca chiều");

            if (ndLeTan != null && ndLeTan2 != null && caSang != null && caChieu != null)
            {
                // letan (Ca sáng): DiLam 5 ngày, Tre 1 ngày (15 phút trễ), Vang 1 ngày
                (int d, string tt, int phut)[] leTanCC = {
                    (-7, "DiLam",  0), (-6, "DiLam",  0), (-5, "Tre",  15),
                    (-4, "DiLam",  0), (-3, "Vang",   0), (-2, "DiLam",  0), (-1, "DiLam",  0)
                };
                foreach (var (d, tt, phut) in leTanCC)
                    db.ChamCongs.Add(new ChamCong {
                        MaNguoiDung = ndLeTan.MaNguoiDung,
                        NgayCong    = DateOnly.FromDateTime(DateTime.Today.AddDays(d)),
                        GioVao      = tt == "Vang" ? null : (TimeOnly?)caSang.GioBatDau.AddMinutes(phut),
                        GioRa       = tt == "Vang" ? null : (TimeOnly?)caSang.GioKetThuc,
                        SoCong      = tt == "Vang" ? 0m : 1m,
                        SoPhutTre   = phut,
                        TrangThai   = tt
                    });

                // letan2 (Ca chiều): DiLam 4 ngày, Tre 2 ngày (20 và 35 phút trễ), Vang 1 ngày
                (int d, string tt, int phut)[] leTan2CC = {
                    (-7, "DiLam",  0), (-6, "Tre",  20), (-5, "DiLam",  0),
                    (-4, "DiLam",  0), (-3, "Tre",  35), (-2, "DiLam",  0), (-1, "Vang",   0)
                };
                foreach (var (d, tt, phut) in leTan2CC)
                    db.ChamCongs.Add(new ChamCong {
                        MaNguoiDung = ndLeTan2.MaNguoiDung,
                        NgayCong    = DateOnly.FromDateTime(DateTime.Today.AddDays(d)),
                        GioVao      = tt == "Vang" ? null : (TimeOnly?)caChieu.GioBatDau.AddMinutes(phut),
                        GioRa       = tt == "Vang" ? null : (TimeOnly?)caChieu.GioKetThuc,
                        SoCong      = tt == "Vang" ? 0m : 1m,
                        SoPhutTre   = phut,
                        TrangThai   = tt
                    });

                await db.SaveChangesAsync();
            }
        }

        // ── 15. Demo đơn giao việc buồng phòng ───────────────────────────────
        if (!await db.DonGiaoViecs.AnyAsync())
        {
            var phong102 = await db.Phongs.FirstOrDefaultAsync(p => p.SoPhong == "102");
            var phong201 = await db.Phongs.FirstOrDefaultAsync(p => p.SoPhong == "201");
            var now      = DateTime.Now;
            if (phong102 != null)
                db.DonGiaoViecs.Add(new DonGiaoViec {
                    MaPhong   = phong102.MaPhong,
                    NoiDung   = "Nhân viên Lan dọn phòng 102: thay ga giường, khăn tắm, vệ sinh phòng tắm, bổ sung minibar.",
                    TrangThai = "ChoLam",
                    NgayGiao  = now
                });
            if (phong201 != null)
                db.DonGiaoViecs.Add(new DonGiaoViec {
                    MaPhong          = phong201.MaPhong,
                    NoiDung          = "Nhân viên Minh dọn phòng 201 sau check-out: vệ sinh toàn bộ, kiểm tra minibar và TV.",
                    TrangThai        = "HoanThanh",
                    NgayGiao         = now.AddDays(-20),
                    NgayHoanThanh    = now.AddDays(-20).AddHours(2),
                    GhiChuNghiemThu  = "Phòng sạch, đủ tiêu chuẩn. Đã bàn giao lễ tân."
                });
            await db.SaveChangesAsync();
        }
    }

    // Tạo user (nếu chưa có) + gán role + tạo/cập nhật NguoiDung kèm theo
    public static async Task<DatPhongKhachSanUser?> SeedUserAsync(
        UserManager<DatPhongKhachSanUser> userManager,
        DatPhongKhachSanContext db,
        string email, string password, string hoTen, string role,
        string? soDienThoai = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new DatPhongKhachSanUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded) return null;
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);

        // Đồng bộ VaiTro trong NguoiDung
        var vaiTro = role == "Admin" ? "Admin" : role == "NhanVien" ? "NhanVien" : "KhachHang";
        var nd = await db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == user.Id);
        if (nd == null)
        {
            db.NguoiDungs.Add(new NguoiDung
            {
                UserId      = user.Id,
                HoTen       = hoTen,
                Email       = email,
                SoDienThoai = soDienThoai,
                VaiTro      = vaiTro,
                PhanLoai    = "Thuong"
            });
        }
        else
        {
            nd.VaiTro = vaiTro;   // đồng bộ nếu đã có
            nd.HoTen  = hoTen;
        }
        await db.SaveChangesAsync();

        return user;
    }
}
