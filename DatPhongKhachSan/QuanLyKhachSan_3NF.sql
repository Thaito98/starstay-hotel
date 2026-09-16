-- =====================================================================
-- WEB QUẢN LÝ KHÁCH SẠN NỘI BỘ - SCHEMA CHUẨN 3NF
-- DB: DatPhongKhachSan (đã có sẵn). DROP bảng nghiệp vụ cũ -> tạo lại bảng mới.
-- GIỮ NGUYÊN 8 bảng Identity + __EFMigrationsHistory.
-- Toàn bộ tên bảng / cột bằng tiếng Việt theo phong cách plan.md.
-- =====================================================================

-- ---------------------------------------------------------------------
-- TẠO DATABASE (chạy phần này TRƯỚC khi scaffold Identity nếu DB chưa có).
-- Nếu DB đã tồn tại (đã scaffold Identity), BỎ QUA khối CREATE DATABASE,
-- chỉ chạy USE rồi tạo các bảng nghiệp vụ bên dưới.
-- ---------------------------------------------------------------------
-- DÙNG DATABASE CÓ SẴN: DatPhongKhachSan
-- (DB đã kết nối trong SSMS, đã có 8 bảng Identity + bảng nghiệp vụ cũ.)
-- Script này: DROP các bảng nghiệp vụ (cũ + mới nếu chạy lại) rồi tạo lại.
-- KHÔNG đụng 8 bảng Identity và __EFMigrationsHistory.
-- ---------------------------------------------------------------------
USE DatPhongKhachSan;
GO

-- ---------------------------------------------------------------------
-- DROP THEO THỨ TỰ PHỤ THUỘC: bảng con (có FK) DROP trước, bảng cha sau.
-- Dùng IF OBJECT_ID(...) IS NOT NULL để chạy được dù bảng có hay không.
-- Gồm cả các bảng nghiệp vụ CŨ (plan cũ) lẫn bảng MỚI (chạy lại lần 2).
-- ---------------------------------------------------------------------

-- Nhóm chatbot
IF OBJECT_ID(N'dbo.TinNhanChat', 'U')        IS NOT NULL DROP TABLE dbo.TinNhanChat;
IF OBJECT_ID(N'dbo.DoanVanBan', 'U')         IS NOT NULL DROP TABLE dbo.DoanVanBan;
GO

-- Nhóm buồng phòng (mới)
IF OBJECT_ID(N'dbo.DonGiaoViec', 'U')        IS NOT NULL DROP TABLE dbo.DonGiaoViec;
GO

-- Nhóm thanh toán & chi tiết đặt phòng (con của DatPhong)
IF OBJECT_ID(N'dbo.ThanhToan', 'U')          IS NOT NULL DROP TABLE dbo.ThanhToan;
IF OBJECT_ID(N'dbo.ChiTietDichVu', 'U')      IS NOT NULL DROP TABLE dbo.ChiTietDichVu;
IF OBJECT_ID(N'dbo.ChiTietDatPhong', 'U')    IS NOT NULL DROP TABLE dbo.ChiTietDatPhong;
GO

-- Bảng đặt phòng
IF OBJECT_ID(N'dbo.DatPhong', 'U')           IS NOT NULL DROP TABLE dbo.DatPhong;
GO

-- Nhóm danh mục phòng / tiện nghi / dịch vụ
IF OBJECT_ID(N'dbo.LoaiPhong_TienNghi', 'U') IS NOT NULL DROP TABLE dbo.LoaiPhong_TienNghi;
IF OBJECT_ID(N'dbo.AnhPhong', 'U')           IS NOT NULL DROP TABLE dbo.AnhPhong;
IF OBJECT_ID(N'dbo.Phong', 'U')              IS NOT NULL DROP TABLE dbo.Phong;
IF OBJECT_ID(N'dbo.TienNghi', 'U')           IS NOT NULL DROP TABLE dbo.TienNghi;
IF OBJECT_ID(N'dbo.DichVu', 'U')             IS NOT NULL DROP TABLE dbo.DichVu;
IF OBJECT_ID(N'dbo.LoaiPhong', 'U')          IS NOT NULL DROP TABLE dbo.LoaiPhong;
GO

-- Nhóm HR (mới)
IF OBJECT_ID(N'dbo.ChamCong', 'U')           IS NOT NULL DROP TABLE dbo.ChamCong;
IF OBJECT_ID(N'dbo.PhanCa', 'U')             IS NOT NULL DROP TABLE dbo.PhanCa;
IF OBJECT_ID(N'dbo.CaLamViec', 'U')          IS NOT NULL DROP TABLE dbo.CaLamViec;
GO

-- Bảng người dùng (cha của nhiều bảng) - DROP cuối cùng
IF OBJECT_ID(N'dbo.NguoiDung', 'U')          IS NOT NULL DROP TABLE dbo.NguoiDung;
GO

-- LƯU Ý: các bảng nghiệp vụ dưới đây tham chiếu AspNetUsers (Identity).
-- => 8 bảng Identity đã có sẵn (giữ nguyên, không drop).

-- =====================================================================
-- NHÓM 1: NGƯỜI DÙNG (mọi người) & PHÂN CA / CHẤM CÔNG
-- =====================================================================

-- Hồ sơ MỌI NGƯỜI: khách hàng, nhân viên (lễ tân), admin.
-- VaiTro phân biệt: "KhachHang" (mặc định khi đăng ký) / "NhanVien" / "Admin".
-- Dùng SONG SONG với Identity Role: Role để [Authorize] phân quyền, VaiTro để hiển thị/lọc.
--   * Có tài khoản  -> UserId trỏ AspNetUsers.Id.
--   * Khách walk-in -> UserId = NULL (không cần tài khoản).
CREATE TABLE NguoiDung (
    MaNguoiDung     INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UserId          NVARCHAR(450)   NULL,                   -- AspNetUsers.Id; NULL nếu walk-in
    HoTen           NVARCHAR(150)   NOT NULL,
    NgaySinh        DATE            NULL,
    GioiTinh        NVARCHAR(10)    NULL,                   -- "Nam","Nu","Khac"
    SoDienThoai     NVARCHAR(20)    NULL,
    Email           NVARCHAR(150)   NULL,
    CCCD            NVARCHAR(20)    NULL,
    DiaChi          NVARCHAR(255)   NULL,
    DuongDanAnh     NVARCHAR(500)   NULL,
    VaiTro          NVARCHAR(20)    NOT NULL DEFAULT N'KhachHang', -- "KhachHang"(mặc định)/"NhanVien"/"Admin"
    PhanLoai        NVARCHAR(20)    NOT NULL DEFAULT N'Thuong', -- phân loại KHÁCH: "Thuong","VIP","Corporate"
    NgayTao         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_NguoiDung_AspNetUsers
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
);
GO
-- Mỗi tài khoản chỉ ứng 1 hồ sơ; nhiều walk-in cùng NULL nên dùng filtered unique.
CREATE UNIQUE INDEX UX_NguoiDung_UserId
    ON NguoiDung(UserId) WHERE UserId IS NOT NULL;
GO

-- Ca làm việc (danh mục ca: Sáng/Chiều/Tối...).
CREATE TABLE CaLamViec (
    MaCa            INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TenCa           NVARCHAR(50)    NOT NULL,               -- "Ca sáng"
    GioBatDau       TIME            NOT NULL,
    GioKetThuc      TIME            NOT NULL
);
GO

-- Phân ca: NHÂN VIÊN (NguoiDung có VaiTro='NhanVien') làm ca nào ngày nào (n-n).
-- Trang phân ca trong khu admin, chỉ role NhanVien/Admin xem ([Authorize], không ở DB).
-- PK tổ hợp (MaNguoiDung, NgayLam, MaCa) -> 1 NV không trùng ca trong cùng ngày.
CREATE TABLE PhanCa (
    MaNguoiDung     INT             NOT NULL,               -- NguoiDung của nhân viên
    NgayLam         DATE            NOT NULL,
    MaCa            INT             NOT NULL,
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT PK_PhanCa PRIMARY KEY (MaNguoiDung, NgayLam, MaCa),
    CONSTRAINT FK_PhanCa_NguoiDung
        FOREIGN KEY (MaNguoiDung) REFERENCES NguoiDung(MaNguoiDung) ON DELETE CASCADE,
    CONSTRAINT FK_PhanCa_CaLamViec
        FOREIGN KEY (MaCa) REFERENCES CaLamViec(MaCa)
);
GO

-- Chấm công: công của 1 NHÂN VIÊN (NguoiDung) trong 1 ngày.
-- Giờ vào/ra là dữ liệu đầu vào, không tách bảng riêng.
CREATE TABLE ChamCong (
    MaChamCong      INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaNguoiDung     INT             NOT NULL,               -- NguoiDung của nhân viên
    NgayCong        DATE            NOT NULL,
    GioVao          TIME            NULL,
    GioRa           TIME            NULL,
    SoCong          DECIMAL(4,2)    NOT NULL DEFAULT 0,     -- 1.0 = đủ công, 0.5 = nửa công (trễ vẫn tính 1.0)
    SoPhutTre       INT             NOT NULL DEFAULT 0,     -- số phút đi trễ (0 nếu đúng giờ); cuối tháng tổng hợp để trừ tiền
    TrangThai       NVARCHAR(20)    NOT NULL DEFAULT N'DiLam', -- "DiLam","Tre","Vang" (Tre vẫn tính đủ công)
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT FK_ChamCong_NguoiDung
        FOREIGN KEY (MaNguoiDung) REFERENCES NguoiDung(MaNguoiDung) ON DELETE CASCADE,
    CONSTRAINT UQ_ChamCong UNIQUE (MaNguoiDung, NgayCong)   -- 1 NV / 1 ngày / 1 dòng
);
GO

-- =====================================================================
-- NHÓM 3: DANH MỤC PHÒNG, TIỆN NGHI, DỊCH VỤ
-- =====================================================================

CREATE TABLE LoaiPhong (
    MaLoaiPhong     INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TenLoaiPhong    NVARCHAR(100)   NOT NULL,
    MoTa            NVARCHAR(MAX)   NULL,
    GiaCoBan        DECIMAL(18,2)   NOT NULL,               -- giá 1 đêm
    SucChuaNguoiLon INT             NOT NULL DEFAULT 2,
    SucChuaTreEm    INT             NOT NULL DEFAULT 1,
    LoaiGiuong      NVARCHAR(50)    NULL,
    DienTich        DECIMAL(5,2)    NULL,
    TrangThai       BIT             NOT NULL DEFAULT 1,
    NgayTao         DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO

CREATE TABLE Phong (
    MaPhong         INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    SoPhong         NVARCHAR(10)    NOT NULL UNIQUE,        -- "101"
    Tang            INT             NULL,
    MaLoaiPhong     INT             NOT NULL,
    TrangThai       NVARCHAR(20)    NOT NULL DEFAULT N'Trong', -- "Trong","DangO","DangDonDep" (dọn dẹp+bảo trì gộp 1)
    GhiChu          NVARCHAR(255)   NULL,
    NgayTao         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Phong_LoaiPhong
        FOREIGN KEY (MaLoaiPhong) REFERENCES LoaiPhong(MaLoaiPhong)
);
GO

CREATE TABLE AnhPhong (
    MaAnh           INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaLoaiPhong     INT             NOT NULL,
    DuongDanAnh     NVARCHAR(500)   NOT NULL,
    LaAnhChinh      BIT             NOT NULL DEFAULT 0,
    ThuTu           INT             NOT NULL DEFAULT 0,
    CONSTRAINT FK_AnhPhong_LoaiPhong
        FOREIGN KEY (MaLoaiPhong) REFERENCES LoaiPhong(MaLoaiPhong) ON DELETE CASCADE
);
GO

CREATE TABLE TienNghi (
    MaTienNghi      INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TenTienNghi     NVARCHAR(100)   NOT NULL,
    BieuTuong       NVARCHAR(100)   NULL,                   -- "fa-wifi"
    MoTa            NVARCHAR(255)   NULL
);
GO

-- Bảng nối n-n giữa LoaiPhong và TienNghi (PK tổ hợp).
CREATE TABLE LoaiPhong_TienNghi (
    MaLoaiPhong     INT             NOT NULL,
    MaTienNghi      INT             NOT NULL,
    CONSTRAINT PK_LoaiPhong_TienNghi PRIMARY KEY (MaLoaiPhong, MaTienNghi),
    CONSTRAINT FK_LPTN_LoaiPhong
        FOREIGN KEY (MaLoaiPhong) REFERENCES LoaiPhong(MaLoaiPhong) ON DELETE CASCADE,
    CONSTRAINT FK_LPTN_TienNghi
        FOREIGN KEY (MaTienNghi) REFERENCES TienNghi(MaTienNghi) ON DELETE CASCADE
);
GO

CREATE TABLE DichVu (
    MaDichVu        INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TenDichVu       NVARCHAR(100)   NOT NULL,
    MoTa            NVARCHAR(MAX)   NULL,
    Gia             DECIMAL(18,2)   NOT NULL,
    DonVi           NVARCHAR(50)    NULL,                   -- "người","lượt","ngày"
    DuongDanAnh     NVARCHAR(500)   NULL,
    TrangThai       BIT             NOT NULL DEFAULT 1
);
GO

-- =====================================================================
-- NHÓM 4: ĐẶT PHÒNG & THANH TOÁN
-- =====================================================================

-- DatPhong trỏ MaNguoiDung -> online & walk-in thống nhất 1 quan hệ.
-- NguonDat cho biết khách đặt online hay lễ tân nhập tại quầy.
-- 3NF: KHÔNG lưu TongTien/ThanhTien/SoTienDaTra/SoTienConLai (đều là giá trị
-- tính được xuyên bảng -> derived). Tính khi cần:
--   TongTien     = SUM(ChiTietDatPhong.ThanhTien) + SUM(ChiTietDichVu.ThanhTien)
--   SoTienDaTra  = SUM(ThanhToan.SoTien WHERE ThanhCong, không phải HoanTien)
--   SoTienConLai = TongTien - SoTienDaTra
-- Trong C# dùng property [NotMapped] hoặc tạo SQL View để tổng hợp.
CREATE TABLE DatPhong (
    MaDatPhong      INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaNguoiDung     INT             NOT NULL,               -- người lưu trú (khách)
    MaDon           NVARCHAR(20)    NOT NULL UNIQUE,        -- "DP20251201..."
    NguonDat        NVARCHAR(10)    NOT NULL DEFAULT N'Online', -- "Online","WalkIn" (walk-in: chỉ lễ tân/admin tạo)
    NgayNhanPhong   DATE            NOT NULL,
    NgayTraPhong    DATE            NOT NULL,
    SoDem           AS DATEDIFF(DAY, NgayNhanPhong, NgayTraPhong),  -- computed cùng hàng (hợp lệ 3NF)
    TongSoNguoiLon  INT             NOT NULL DEFAULT 1,
    TongSoTreEm     INT             NOT NULL DEFAULT 0,
    LoaiThanhToan   NVARCHAR(20)    NOT NULL DEFAULT N'ThanhToanDu', -- "ThanhToanDu","Coc30" (lựa chọn lúc đặt = gốc)
    TrangThai       NVARCHAR(20)    NOT NULL DEFAULT N'ChoXacNhan',
        -- "ChoXacNhan","DaXacNhan","DangO","DaTraPhong","ChoHoanTien","DaHuy"
    GhiChu          NVARCHAR(MAX)   NULL,
    NgayDat         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    NgayCapNhat     DATETIME2       NULL,
    NgayHuy         DATETIME2       NULL,
    CONSTRAINT FK_DatPhong_NguoiDung
        FOREIGN KEY (MaNguoiDung) REFERENCES NguoiDung(MaNguoiDung),
    CONSTRAINT CK_DatPhong_Ngay CHECK (NgayTraPhong > NgayNhanPhong)
);
GO

-- Mỗi phòng trong đơn = 1 dòng. Hệ thống gán phòng cụ thể (MaPhong) ngay lúc đặt
-- (tìm phòng trống cùng loại theo ngày). Loại phòng suy ra qua Phong -> LoaiPhong.
-- GiaMotDem là SNAPSHOT giá lúc đặt (gốc, vì giá có thể đổi sau này).
-- KHÔNG lưu ThanhTien: = GiaMotDem * DatPhong.SoDem -> derived xuyên bảng, tính bằng code.
CREATE TABLE ChiTietDatPhong (
    MaChiTiet       INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaDatPhong      INT             NOT NULL,
    MaPhong         INT             NOT NULL,               -- số phòng cụ thể (gán ngay lúc đặt)
    SoNguoiLon      INT             NOT NULL DEFAULT 1,
    SoTreEm         INT             NOT NULL DEFAULT 0,
    GiaMotDem       DECIMAL(18,2)   NOT NULL,               -- SNAPSHOT giá lúc đặt (gốc)
    TenKhachLuuTru  NVARCHAR(150)   NULL,
    CONSTRAINT FK_CTDP_DatPhong
        FOREIGN KEY (MaDatPhong) REFERENCES DatPhong(MaDatPhong) ON DELETE CASCADE,
    CONSTRAINT FK_CTDP_Phong
        FOREIGN KEY (MaPhong) REFERENCES Phong(MaPhong)
);
GO

-- Dịch vụ gắn vào TỪNG PHÒNG trong đơn. Snapshot đơn giá.
CREATE TABLE ChiTietDichVu (
    MaChiTietDV     INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaChiTiet       INT             NOT NULL,               -- trỏ ChiTietDatPhong
    MaDichVu        INT             NOT NULL,
    SoLuong         INT             NOT NULL DEFAULT 1,
    DonGia          DECIMAL(18,2)   NOT NULL,               -- SNAPSHOT đơn giá lúc đặt (gốc)
    ThanhTien       AS (SoLuong * DonGia),                  -- computed CÙNG HÀNG -> hợp lệ 3NF
    CONSTRAINT FK_CTDV_ChiTietDatPhong
        FOREIGN KEY (MaChiTiet) REFERENCES ChiTietDatPhong(MaChiTiet) ON DELETE CASCADE,
    CONSTRAINT FK_CTDV_DichVu
        FOREIGN KEY (MaDichVu) REFERENCES DichVu(MaDichVu)
);
GO

-- Ghi nhận giao dịch thanh toán (VNPay đặt cọc / thanh toán đủ / hoàn tiền).
CREATE TABLE ThanhToan (
    MaThanhToan     INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaDatPhong      INT             NOT NULL,
    SoTien          DECIMAL(18,2)   NOT NULL,
    PhuongThuc      NVARCHAR(30)    NOT NULL,               -- "VNPay","TienMat","ChuyenKhoan"
    LoaiThanhToan   NVARCHAR(20)    NOT NULL DEFAULT N'ThanhToanDu', -- "DatCoc","ThanhToanDu","HoanTien"
    MaGiaoDich      NVARCHAR(100)   NULL,                   -- mã từ VNPay
    TrangThai       NVARCHAR(20)    NOT NULL DEFAULT N'ChoXuLy', -- "ChoXuLy","ThanhCong","ThatBai","DaHoan"
    NgayThanhToan   DATETIME2       NULL,
    GhiChu          NVARCHAR(255)   NULL,
    NgayTao         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ThanhToan_DatPhong
        FOREIGN KEY (MaDatPhong) REFERENCES DatPhong(MaDatPhong) ON DELETE CASCADE
);
GO

-- =====================================================================
-- NHÓM 5: BUỒNG PHÒNG (đơn giao việc dọn/sửa)
-- =====================================================================

-- Đơn giao việc dọn/sửa GẮN VỚI 1 PHÒNG cụ thể. Tạo khi phòng chuyển "DangDonDep"
-- (sau check-out). Lễ tân ghi việc + tên lao công vào NoiDung, cập nhật & nghiệm thu.
-- KHÔNG lưu người giao: phân quyền [Authorize] đã giới hạn chỉ NhanVien/Admin tạo được.
-- Lao công KHÔNG dùng web (tên họ nằm trong NoiDung).
CREATE TABLE DonGiaoViec (
    MaDonViec       INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaPhong         INT             NOT NULL,               -- phòng cần dọn/sửa
    NoiDung         NVARCHAR(MAX)   NOT NULL,               -- "Nhân viên A dọn phòng, thay ga" / "Sửa máy lạnh"
    TrangThai       NVARCHAR(20)    NOT NULL DEFAULT N'ChoLam', -- "ChoLam","HoanThanh"
    NgayGiao        DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    NgayHoanThanh   DATETIME2       NULL,
    GhiChuNghiemThu NVARCHAR(MAX)   NULL,                   -- lễ tân ghi khi nghiệm thu (truy vết khi khách phàn nàn)
    CONSTRAINT FK_DonGiaoViec_Phong
        FOREIGN KEY (MaPhong) REFERENCES Phong(MaPhong)
);
GO

-- =====================================================================
-- NHÓM 6: CHATBOT RAG
-- =====================================================================

CREATE TABLE DoanVanBan (
    MaDoan          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CauHoi          NVARCHAR(MAX)   NOT NULL,
    TraLoi          NVARCHAR(MAX)   NOT NULL,
    ChuDe           NVARCHAR(100)   NULL,                   -- "CheckIn","NoiQuy"
    Embedding       VARBINARY(MAX)  NULL,                   -- vector 1024 chiều BGE-M3
    ModelEmbedding  NVARCHAR(100)   NULL,                   -- model đã dùng để sinh embedding
    NgayIndex       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
    -- Bảng độc lập, KHÔNG có FK.
);
GO

CREATE TABLE TinNhanChat (
    MaTinNhan       INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    MaPhienChat     UNIQUEIDENTIFIER NOT NULL,              -- GUID gom tin 1 phiên
    UserId          NVARCHAR(450)   NULL,                   -- NULL nếu guest chưa login
    VaiTro          NVARCHAR(10)    NOT NULL,               -- "User","Bot"
    NoiDung         NVARCHAR(MAX)   NOT NULL,
    LoaiCauTraLoi   NVARCHAR(20)    NULL,                   -- "FAQ","GiaPhong","PhongTrong",...
    NgayTao         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_TinNhanChat_AspNetUsers
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
);
GO

-- =====================================================================
-- GHI CHÚ: TIỀN ĐƠN TÍNH BẰNG CODE (KHÔNG lưu cột, KHÔNG cần View)
-- Tính trong C#/EF Core bằng LINQ khi cần hiển thị / báo cáo:
--   TongTien     = SUM(ChiTietDatPhong.GiaMotDem * DatPhong.SoDem)
--                + SUM(ChiTietDichVu.ThanhTien)
--   SoTienDaTra  = SUM(ThanhToan.SoTien) WHERE TrangThai='ThanhCong'
--                  AND LoaiThanhToan <> 'HoanTien'
--   SoTienConLai = TongTien - SoTienDaTra
-- =====================================================================

-- (Chỉ mục/index sẽ tạo sau khi cần tối ưu truy vấn - chưa tạo ở bước này.)
