using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Data;

public class DatPhongKhachSanContext : IdentityDbContext<DatPhongKhachSanUser>
{
    public DatPhongKhachSanContext(DbContextOptions<DatPhongKhachSanContext> options)
        : base(options)
    {
    }

    // 17 bảng nghiệp vụ
    public virtual DbSet<AnhPhong> AnhPhongs { get; set; }
    public virtual DbSet<CaLamViec> CaLamViecs { get; set; }
    public virtual DbSet<ChamCong> ChamCongs { get; set; }
    public virtual DbSet<ChiTietDatPhong> ChiTietDatPhongs { get; set; }
    public virtual DbSet<ChiTietDichVu> ChiTietDichVus { get; set; }
    public virtual DbSet<DatPhong> DatPhongs { get; set; }
    public virtual DbSet<DichVu> DichVus { get; set; }
    public virtual DbSet<DoanVanBan> DoanVanBans { get; set; }
    public virtual DbSet<DonGiaoViec> DonGiaoViecs { get; set; }
    public virtual DbSet<LoaiPhong> LoaiPhongs { get; set; }
    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }
    public virtual DbSet<PhanCa> PhanCas { get; set; }
    public virtual DbSet<Phong> Phongs { get; set; }
    public virtual DbSet<ThanhToan> ThanhToans { get; set; }
    public virtual DbSet<TienNghi> TienNghis { get; set; }
    public virtual DbSet<TinNhanChat> TinNhanChats { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AnhPhong>(entity =>
        {
            entity.HasKey(e => e.MaAnh).HasName("PK__AnhPhong__356240DF598748C0");
            entity.HasOne(d => d.MaLoaiPhongNavigation).WithMany(p => p.AnhPhongs)
                .HasConstraintName("FK_AnhPhong_LoaiPhong");
        });

        builder.Entity<CaLamViec>(entity =>
        {
            entity.HasKey(e => e.MaCa).HasName("PK__CaLamVie__27258E7BE91E0F69");
        });

        builder.Entity<ChamCong>(entity =>
        {
            entity.HasKey(e => e.MaChamCong).HasName("PK__ChamCong__307331A1D52B03F9");
            entity.Property(e => e.TrangThai).HasDefaultValue("DiLam");
            entity.Property(e => e.SoPhutTre).HasDefaultValue(0);
        });

        builder.Entity<ChiTietDatPhong>(entity =>
        {
            entity.HasKey(e => e.MaChiTiet).HasName("PK__ChiTietD__CDF0A11436D83325");
            entity.Property(e => e.SoNguoiLon).HasDefaultValue(1);
            entity.HasOne(d => d.MaDatPhongNavigation).WithMany(p => p.ChiTietDatPhongs)
                .HasConstraintName("FK_CTDP_DatPhong");
            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.ChiTietDatPhongs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CTDP_Phong");
        });

        builder.Entity<ChiTietDichVu>(entity =>
        {
            entity.HasKey(e => e.MaChiTietDV).HasName("PK__ChiTietD__651E6E58EE2C41D8");
            entity.Property(e => e.SoLuong).HasDefaultValue(1);
            entity.Property(e => e.ThanhTien).HasComputedColumnSql("([SoLuong]*[DonGia])", false);
            entity.HasOne(d => d.MaChiTietNavigation).WithMany(p => p.ChiTietDichVus)
                .HasConstraintName("FK_CTDV_ChiTietDatPhong");
            entity.HasOne(d => d.MaDichVuNavigation).WithMany(p => p.ChiTietDichVus)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CTDV_DichVu");
        });

        builder.Entity<DatPhong>(entity =>
        {
            entity.HasKey(e => e.MaDatPhong).HasName("PK__DatPhong__6344ADEA7AE60980");
            entity.Property(e => e.LoaiThanhToan).HasDefaultValue("ThanhToanDu");
            entity.Property(e => e.NgayDat).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NguonDat).HasDefaultValue("Online");
            entity.Property(e => e.SoDem).HasComputedColumnSql("(datediff(day,[NgayNhanPhong],[NgayTraPhong]))", false);
            entity.Property(e => e.TongSoNguoiLon).HasDefaultValue(1);
            entity.Property(e => e.TrangThai).HasDefaultValue("ChoXacNhan");
            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.DatPhongs)
                .HasForeignKey(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatPhong_NguoiDung");
        });

        builder.Entity<DichVu>(entity =>
        {
            entity.HasKey(e => e.MaDichVu).HasName("PK__DichVu__C0E6DE8F11D396DA");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        builder.Entity<DoanVanBan>(entity =>
        {
            entity.HasKey(e => e.MaDoan).HasName("PK__DoanVanB__2DC20C5F45231DDD");
            entity.Property(e => e.NgayIndex).HasDefaultValueSql("(sysdatetime())");
        });

        builder.Entity<DonGiaoViec>(entity =>
        {
            entity.HasKey(e => e.MaDonViec).HasName("PK__DonGiaoV__9CF68D7F1F1ADAEC");
            entity.Property(e => e.NgayGiao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TrangThai).HasDefaultValue("ChoLam");
        });

        builder.Entity<LoaiPhong>(entity =>
        {
            entity.HasKey(e => e.MaLoaiPhong).HasName("PK__LoaiPhon__23021217DD2580B0");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.SucChuaNguoiLon).HasDefaultValue(2);
            entity.Property(e => e.SucChuaTreEm).HasDefaultValue(1);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
            entity.HasMany(d => d.MaTienNghis).WithMany(p => p.MaLoaiPhongs)
                .UsingEntity<Dictionary<string, object>>(
                    "LoaiPhong_TienNghi",
                    r => r.HasOne<TienNghi>().WithMany()
                        .HasForeignKey("MaTienNghi")
                        .HasConstraintName("FK_LPTN_TienNghi"),
                    l => l.HasOne<LoaiPhong>().WithMany()
                        .HasForeignKey("MaLoaiPhong")
                        .HasConstraintName("FK_LPTN_LoaiPhong"),
                    j =>
                    {
                        j.HasKey("MaLoaiPhong", "MaTienNghi");
                        j.ToTable("LoaiPhong_TienNghi");
                    });
        });

        builder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__NguoiDun__C539D762CA878136");
            entity.HasIndex(e => e.UserId, "UX_NguoiDung_UserId")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.VaiTro).HasDefaultValue("KhachHang");
            entity.Property(e => e.PhanLoai).HasDefaultValue("Thuong");
        });

        builder.Entity<PhanCa>(entity =>
        {
            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.PhanCas)
                .HasForeignKey(d => d.MaNguoiDung)
                .HasConstraintName("FK_PhanCa_NguoiDung");
            entity.HasOne(d => d.MaCaNavigation).WithMany(p => p.PhanCas)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PhanCa_CaLamViec");
        });

        builder.Entity<ChamCong>(entity =>
        {
            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.ChamCongs)
                .HasForeignKey(d => d.MaNguoiDung)
                .HasConstraintName("FK_ChamCong_NguoiDung");
        });

        builder.Entity<DonGiaoViec>(entity =>
        {
            entity.HasKey(e => e.MaDonViec);
            entity.Property(e => e.NgayGiao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TrangThai).HasDefaultValue("ChoLam");
            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.DonGiaoViecs)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DonGiaoViec_Phong");
        });

        builder.Entity<Phong>(entity =>
        {
            entity.HasKey(e => e.MaPhong).HasName("PK__Phong__20BD5E5B0EBC1201");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TrangThai).HasDefaultValue("Trong");
            entity.HasOne(d => d.MaLoaiPhongNavigation).WithMany(p => p.Phongs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Phong_LoaiPhong");
        });

        builder.Entity<ThanhToan>(entity =>
        {
            entity.HasKey(e => e.MaThanhToan).HasName("PK__ThanhToa__D4B2584425E6621D");
            entity.Property(e => e.LoaiThanhToan).HasDefaultValue("ThanhToanDu");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TrangThai).HasDefaultValue("ChoXuLy");
            entity.HasOne(d => d.MaDatPhongNavigation).WithMany(p => p.ThanhToans)
                .HasConstraintName("FK_ThanhToan_DatPhong");
        });

        builder.Entity<TienNghi>(entity =>
        {
            entity.HasKey(e => e.MaTienNghi).HasName("PK__TienNghi__ED7B8F4D6D207CCA");
        });

        builder.Entity<TinNhanChat>(entity =>
        {
            entity.HasKey(e => e.MaTinNhan).HasName("PK__TinNhanC__E5B3062A72D27223");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysdatetime())");
        });
    }
}
