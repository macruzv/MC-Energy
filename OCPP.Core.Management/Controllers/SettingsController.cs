using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using OCPP.Core.Management.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace OCPP.Core.Management.Controllers
{
    public class SettingsController : BaseController
    {
        private readonly DataMigrationService _migrationService;
        private readonly IWebHostEnvironment _env;

        public SettingsController(
            UserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext,
            DataMigrationService migrationService,
            IWebHostEnvironment env)
            : base(userManager, loggerFactory, config, dbContext)
        {
            _migrationService = migrationService;
            _env = env;
            Logger = loggerFactory.CreateLogger<SettingsController>();
        }

        public IActionResult Index()
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return RedirectToAction("Index", "Home");

            var priceSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "PricePerKWh");
            ViewBag.PricePerKWh = priceSetting?.Value ?? "0.00";

            var nameSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyName");
            ViewBag.CompanyName = nameSetting?.Value ?? "ENERGY CORE MC";

            var addressSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyAddress");
            ViewBag.CompanyAddress = addressSetting?.Value ?? "Estación de Carga Eléctrica";

            var branchSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyBranch");
            ViewBag.CompanyBranch = branchSetting?.Value ?? "";

            // Printer Settings
            ViewBag.PrinterMode = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_Mode")?.Value ?? "local";
            ViewBag.PrinterIP = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_IP")?.Value ?? "";
            ViewBag.PrinterPort = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_Port")?.Value ?? "9100";
            ViewBag.PrinterDPI = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_DPI")?.Value ?? "150";
            ViewBag.PrinterWidth = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_Width")?.Value ?? "56";

            ViewBag.IsSqlServer = DbContext.Database.IsSqlServer();

            // Billing Settings
            ViewBag.BillingMode = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Billing_Mode")?.Value ?? "Energy";
            ViewBag.PricingType = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Type")?.Value ?? "Fixed";
            ViewBag.PricingSchedules = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Schedules")?.Value ?? "[]";
            ViewBag.UsageFee = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "UsageFee")?.Value ?? "0.00";

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SavePrinterSettings(string printerMode, string printerIP, string printerPort, string printerDPI, string printerWidth)
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            var settingsToUpdate = new Dictionary<string, string>
            {
                { "Printer_Mode", printerMode },
                { "Printer_IP", printerIP },
                { "Printer_Port", printerPort },
                { "Printer_DPI", printerDPI },
                { "Printer_Width", printerWidth }
            };

            foreach (var item in settingsToUpdate)
            {
                var setting = await DbContext.SystemSettings.FirstOrDefaultAsync(s => s.SettingId == item.Key);
                if (setting == null)
                {
                    DbContext.SystemSettings.Add(new SystemSetting { SettingId = item.Key, Value = item.Value });
                }
                else
                {
                    setting.Value = item.Value;
                }
            }

            await DbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Configuración de impresión guardada correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SavePrice(string price)
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            var priceSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "PricePerKWh");
            if (priceSetting == null)
            {
                priceSetting = new SystemSetting { SettingId = "PricePerKWh", Value = price };
                DbContext.SystemSettings.Add(priceSetting);
            }
            else
            {
                priceSetting.Value = price;
            }

            await DbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Precio por kWh actualizado.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SaveBillingSettings(string billingMode, string pricingType, string pricingSchedules, string usageFee, string price)
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            var settingsToUpdate = new Dictionary<string, string>
            {
                { "Billing_Mode", billingMode },
                { "Pricing_Type", pricingType },
                { "Pricing_Schedules", pricingSchedules },
                { "UsageFee", usageFee },
                { "PricePerKWh", price }
            };

            foreach (var item in settingsToUpdate)
            {
                var setting = await DbContext.SystemSettings.FirstOrDefaultAsync(s => s.SettingId == item.Key);
                if (setting == null)
                {
                    DbContext.SystemSettings.Add(new SystemSetting { SettingId = item.Key, Value = item.Value });
                }
                else
                {
                    setting.Value = item.Value;
                }
            }

            await DbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Configuración de facturación guardada correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SaveCompany(string companyName, string companyAddress, string companyBranch)
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            var nameSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyName");
            if (nameSetting == null)
            {
                nameSetting = new SystemSetting { SettingId = "CompanyName", Value = companyName };
                DbContext.SystemSettings.Add(nameSetting);
            }
            else
            {
                nameSetting.Value = companyName;
            }

            var addressSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyAddress");
            if (addressSetting == null)
            {
                addressSetting = new SystemSetting { SettingId = "CompanyAddress", Value = companyAddress };
                DbContext.SystemSettings.Add(addressSetting);
            }
            else
            {
                addressSetting.Value = companyAddress;
            }

            var branchSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyBranch");
            if (branchSetting == null)
            {
                branchSetting = new SystemSetting { SettingId = "CompanyBranch", Value = companyBranch };
                DbContext.SystemSettings.Add(branchSetting);
            }
            else
            {
                branchSetting.Value = companyBranch;
            }

            await DbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Datos de la empresa actualizados.";
            return RedirectToAction("Index");
        }

        public IActionResult TicketPreview()
        {
            if (!User.IsInRole(Constants.AdminRoleName)) return RedirectToAction("Index", "Home");

            // Crear una transacción ficticia para la previsualización
            var dummyTx = new Transaction
            {
                TransactionId = 0,
                ChargePointId = "PRUEBA-PDA",
                ConnectorId = 1,
                StartTime = DateTime.Now.AddMinutes(-30),
                StopTime = DateTime.Now,
                MeterStart = 100000,
                MeterStop = 112500 // 12.5 kWh
            };

            var priceSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "PricePerKWh");
            ViewBag.PricePerKWh = priceSetting?.Value ?? "0.00";
            
            ViewBag.BillingMode = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Billing_Mode")?.Value ?? "Energy";
            ViewBag.PricingType = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Type")?.Value ?? "Fixed";
            ViewBag.PricingSchedules = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Schedules")?.Value ?? "[]";
            ViewBag.UsageFee = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "UsageFee")?.Value ?? "5.00"; // Default demo value if missing
            ViewBag.CustomerName = "CLIENTE DE PRUEBA (DEMO)";

            var nameSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyName");
            ViewBag.CompanyName = nameSetting?.Value ?? "ENERGY CORE MC";

            var addressSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyAddress");
            ViewBag.CompanyAddress = addressSetting?.Value ?? "Estación de Carga Eléctrica";

            var branchSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyBranch");
            ViewBag.CompanyBranch = branchSetting?.Value ?? "";

            // Try to find a real charge point to simulate a more realistic ticket
            // Prioritize one that has a Branch defined so we can see it on the ticket
            var realChargePoint = DbContext.ChargePoints.FirstOrDefault(cp => !string.IsNullOrEmpty(cp.Branch));
            
            // If no charger with branch, just take any charger
            if (realChargePoint == null)
            {
                realChargePoint = DbContext.ChargePoints.FirstOrDefault();
            }

            if (realChargePoint != null)
            {
                dummyTx.ChargePointId = realChargePoint.ChargePointId;
                if (!string.IsNullOrEmpty(realChargePoint.Branch))
                {
                     ViewBag.CompanyBranch = realChargePoint.Branch;
                }
            }

            ViewBag.PrinterDPI = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_DPI")?.Value ?? "150";
            ViewBag.PrinterWidth = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_Width")?.Value ?? "56";

            return View("../Home/Ticket", dummyTx);
        }

        [HttpPost]
        public async Task<IActionResult> SyncFromProduction()
        {
            try 
            {
                if (DbContext.Database.IsSqlServer()) return BadRequest("La sincronización no está permitida cuando ya se está conectado a SQL Server.");
                if (!_env.IsDevelopment()) return BadRequest("Esta función solo está disponible en desarrollo.");
                if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

                var result = await _migrationService.SyncFromProduction();
                
                if (result.success)
                {
                    TempData["SuccessMessage"] = result.message;
                }
                else
                {
                    TempData["ErrorMessage"] = result.message;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error crítico durante la sincronización.");
                TempData["ErrorMessage"] = $"Error crítico: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
