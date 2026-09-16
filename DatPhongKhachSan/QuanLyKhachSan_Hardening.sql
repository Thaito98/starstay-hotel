-- QuanLyKhachSan_Hardening.sql
-- Run after QuanLyKhachSan_3NF.sql.
-- Group A: indexes and CHECK constraints.
-- Group B: rowversion, cascade review, audit log - reserved for later.

/* Indexes */

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ChiTietDatPhong_MaPhong'
      AND object_id = OBJECT_ID(N'dbo.ChiTietDatPhong')
)
    CREATE INDEX IX_ChiTietDatPhong_MaPhong
    ON dbo.ChiTietDatPhong(MaPhong)
    INCLUDE (MaDatPhong);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ChiTietDatPhong_MaDatPhong'
      AND object_id = OBJECT_ID(N'dbo.ChiTietDatPhong')
)
    CREATE INDEX IX_ChiTietDatPhong_MaDatPhong
    ON dbo.ChiTietDatPhong(MaDatPhong);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DatPhong_Ngay_TrangThai'
      AND object_id = OBJECT_ID(N'dbo.DatPhong')
)
    CREATE INDEX IX_DatPhong_Ngay_TrangThai
    ON dbo.DatPhong(NgayNhanPhong, NgayTraPhong)
    INCLUDE (TrangThai, MaNguoiDung);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DatPhong_TrangThai_NgayDat'
      AND object_id = OBJECT_ID(N'dbo.DatPhong')
)
    CREATE INDEX IX_DatPhong_TrangThai_NgayDat
    ON dbo.DatPhong(TrangThai, NgayDat);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DatPhong_MaNguoiDung'
      AND object_id = OBJECT_ID(N'dbo.DatPhong')
)
    CREATE INDEX IX_DatPhong_MaNguoiDung
    ON dbo.DatPhong(MaNguoiDung);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Phong_MaLoaiPhong'
      AND object_id = OBJECT_ID(N'dbo.Phong')
)
    CREATE INDEX IX_Phong_MaLoaiPhong
    ON dbo.Phong(MaLoaiPhong, TrangThai);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ThanhToan_MaDatPhong'
      AND object_id = OBJECT_ID(N'dbo.ThanhToan')
)
    CREATE INDEX IX_ThanhToan_MaDatPhong
    ON dbo.ThanhToan(MaDatPhong, TrangThai);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TinNhanChat_UserId'
      AND object_id = OBJECT_ID(N'dbo.TinNhanChat')
)
    CREATE INDEX IX_TinNhanChat_UserId
    ON dbo.TinNhanChat(UserId, NgayTao DESC);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_NguoiDung_SoDienThoai'
      AND object_id = OBJECT_ID(N'dbo.NguoiDung')
)
    CREATE INDEX IX_NguoiDung_SoDienThoai
    ON dbo.NguoiDung(SoDienThoai);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ChamCong_NguoiDung_Ngay'
      AND object_id = OBJECT_ID(N'dbo.ChamCong')
)
    CREATE INDEX IX_ChamCong_NguoiDung_Ngay
    ON dbo.ChamCong(MaNguoiDung, NgayCong);

/* CHECK constraints */

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Phong_TrangThai')
    ALTER TABLE dbo.Phong ADD CONSTRAINT CK_Phong_TrangThai
    CHECK (TrangThai IN (N'Trong', N'DangO', N'DangDonDep'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DatPhong_TrangThai')
    ALTER TABLE dbo.DatPhong ADD CONSTRAINT CK_DatPhong_TrangThai
    CHECK (TrangThai IN (N'ChoXacNhan', N'DaXacNhan', N'DangO',
                         N'DaTraPhong', N'ChoHoanTien', N'DaHuy'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DatPhong_NguonDat')
    ALTER TABLE dbo.DatPhong ADD CONSTRAINT CK_DatPhong_NguonDat
    CHECK (NguonDat IN (N'Online', N'WalkIn'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DatPhong_LoaiTT')
    ALTER TABLE dbo.DatPhong ADD CONSTRAINT CK_DatPhong_LoaiTT
    CHECK (LoaiThanhToan IN (N'ThanhToanDu', N'Coc30'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ThanhToan_TrangThai')
    ALTER TABLE dbo.ThanhToan ADD CONSTRAINT CK_ThanhToan_TrangThai
    CHECK (TrangThai IN (N'ChoXuLy', N'ThanhCong', N'ThatBai', N'DaHoan'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ThanhToan_Loai')
    ALTER TABLE dbo.ThanhToan ADD CONSTRAINT CK_ThanhToan_Loai
    CHECK (LoaiThanhToan IN (N'DatCoc', N'ThanhToanDu', N'HoanTien'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ThanhToan_PhuongThuc')
    ALTER TABLE dbo.ThanhToan ADD CONSTRAINT CK_ThanhToan_PhuongThuc
    CHECK (PhuongThuc IN (N'VNPay', N'TienMat', N'ChuyenKhoan'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ThanhToan_SoTien')
    ALTER TABLE dbo.ThanhToan ADD CONSTRAINT CK_ThanhToan_SoTien
    CHECK (SoTien > 0);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DonGiaoViec_TrangThai')
    ALTER TABLE dbo.DonGiaoViec ADD CONSTRAINT CK_DonGiaoViec_TrangThai
    CHECK (TrangThai IN (N'ChoLam', N'HoanThanh'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_NguoiDung_VaiTro')
    ALTER TABLE dbo.NguoiDung ADD CONSTRAINT CK_NguoiDung_VaiTro
    CHECK (VaiTro IN (N'KhachHang', N'NhanVien', N'Admin'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CTDP_Gia')
    ALTER TABLE dbo.ChiTietDatPhong ADD CONSTRAINT CK_CTDP_Gia
    CHECK (GiaMotDem >= 0);

/* Group B placeholder */
