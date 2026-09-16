using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.VnPay;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            builder.Services.AddDbContext<DatPhongKhachSanContext>(options =>
                options.UseSqlServer(connectionString,
                    sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<DatPhongKhachSanUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 6;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<DatPhongKhachSanContext>();

            // ── Policies phân quyền theo Claims (plan.md mục 6.3) ────────────────
            // Mỗi claim "ChucNang" tương ứng 1 policy. Admin tối cao được seed tất cả.
            builder.Services.AddAuthorization(options =>
            {
                foreach (var cn in ChucNang.TatCa)
                    options.AddPolicy(cn, p => p.RequireClaim(ChucNang.ClaimType, cn));
            });

            builder.Services.AddControllersWithViews();

            // ── Cấu hình VnPay ────────────────────────────────────────────────────
            builder.Services.Configure<VnPayConfig>(
                builder.Configuration.GetSection("VnPay"));

            // ── Cấu hình Email ────────────────────────────────────────────────────
            builder.Services.Configure<EmailConfig>(
                builder.Configuration.GetSection("Email"));

            // ── Services ──────────────────────────────────────────────────────────
            builder.Services.AddScoped<DatPhongService>();
            builder.Services.AddScoped<EmailService>();

            // ── AI provider config ─────────────────────────────────────────────
            builder.Services.Configure<AIConfig>(builder.Configuration.GetSection("AI"));

            // ── Chatbot RAG services ───────────────────────────────────────────
            builder.Services.AddHttpClient("ollama", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);  // bge-m3 embedding CPU ~3s
            });
            builder.Services.AddHttpClient("qwen", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(120); // Ollama CPU ~60s; Cloudflare cold start ~30s
            });
            builder.Services.AddScoped<IOllamaService, OllamaService>();
            // OpenAiCompatProvider: một instance/request, shared qua cả 2 interface
            builder.Services.AddScoped<OpenAiCompatProvider>();
            builder.Services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OpenAiCompatProvider>());
            builder.Services.AddScoped<IChatProvider>(sp => sp.GetRequiredService<OpenAiCompatProvider>());
            builder.Services.AddSingleton<EmbeddingCache>();
            builder.Services.AddSingleton<ChatbotService>();
            builder.Services.AddHostedService<DonHetHanCleanupService>();
            // ── Chatbot tools (Phase 19) ───────────────────────────────────────
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ITool,
                                       DatPhongKhachSan.Services.Chatbot.Tools.TraGiaPhongTool>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ITool,
                                       DatPhongKhachSan.Services.Chatbot.Tools.DanhSachDichVuTool>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ITool,
                                       DatPhongKhachSan.Services.Chatbot.Tools.TimFaqTool>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ITool,
                                       DatPhongKhachSan.Services.Chatbot.Tools.KiemTraPhongTrongTool>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ITool,
                                       DatPhongKhachSan.Services.Chatbot.Tools.DonCuaToiTool>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.Tools.ToolRegistry>();
            builder.Services.AddScoped<DatPhongKhachSan.Services.Chatbot.AgentService>();

            // ── Session (lưu MaPhienChat chatbot) ─────────────────────────────
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout     = TimeSpan.FromHours(4);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // ── Data Protection: persist key ra disk ──────────────────────────
            // Thiếu => key sinh mới mỗi lần restart/redeploy => cookie + session hỏng.
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(
                    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                await SeedData.InitializeAsync(scope.ServiceProvider);
            }

            // Load embedding cache (bỏ qua nếu DB trống)
            await app.Services.GetRequiredService<ChatbotService>().LoadCacheAsync();

            // 1. ForwardedHeaders: đứng đầu - reverse proxy (MonsterASP) truyền scheme/IP qua đây.
            //    Phải trước UseHttpsRedirection để Request.Scheme = "https" đúng.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // 2. StaticFiles: đứng sớm - css/js/ảnh không cần auth, không cần redirect.
            //    Đặt trước UseHttpsRedirection tránh redirect loop khi chạy http nội bộ.
            //    Đặt trước UseStatusCodePages tránh nuốt 404 của file tĩnh vào Error page.
            app.UseStaticFiles();

            // 3. Exception handling
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            app.Run();
        }
    }
}
