using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Contracts.Events.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>
/// The catalog and settings of the billing service: products with their two billing settings, entitlements, plans
/// (who buys, seats, which entitlements), the seller and the bank accounts.
/// </summary>
public sealed class BillingCatalog(BillingDbContext db, TimeProvider clock, IBillingEvents? events = null) : IBillingCatalog
{
    public const int MaxFeatures = 20;
    public const int MaxFeatureLength = 200;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Products and entitlements ------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ProductView>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var entitlements = await db.Entitlements.AsNoTracking().OrderBy(item => item.Key).ToListAsync(cancellationToken);
        return products.Select(product => View(product, entitlements)).ToArray();
    }

    public async Task<ProductView> CreateProductAsync(ProductRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var code = BillingRules.NormalizeCode(request.Code)
            ?? throw new BusinessRuleException("Billing.ProductInvalid", "Ürün kodu 2–40 karakter olmalı; küçük harf, rakam ve tire içerebilir.");
        if (await db.Products.AnyAsync(item => item.Code == code, cancellationToken))
            throw new BusinessRuleException("Billing.ProductExists", "Bu ürün kodu zaten var.");
        var product = new Product { Id = Guid.NewGuid(), Code = code, Name = Name(request.Name, "Ürün adı"), IsActive = request.IsActive,
            SortOrder = request.SortOrder, CreatedAt = Now, UpdatedBy = actorId };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return View(product, []);
    }

    /// <summary>The code is the key product services ask with; it never changes.</summary>
    public async Task<ProductView?> UpdateProductAsync(Guid id, ProductRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return null;
        product.Name = Name(request.Name, "Ürün adı");
        product.IsActive = request.IsActive;
        product.SortOrder = request.SortOrder;
        product.UpdatedAt = Now;
        product.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return await ProductAsync(id, cancellationToken);
    }

    public async Task<ProductView?> UpdateProductSettingsAsync(Guid id, ProductSettingsRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.BankTransferApproval))
            throw new BusinessRuleException("Billing.ProductInvalid", "EFT onay türü geçersiz.");
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return null;
        // Products cache the switch; they hear of a change with the transaction that makes it.
        if (product.RequireSubscription != request.RequireSubscription && events is not null)
            await events.PublishAsync(new BillingProductSettingsChangedV1(product.Code, request.RequireSubscription, Now), cancellationToken);
        product.RequireSubscription = request.RequireSubscription;
        product.BankTransferApproval = request.BankTransferApproval;
        product.UpdatedAt = Now;
        product.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return await ProductAsync(id, cancellationToken);
    }

    public async Task<ProductView?> AddEntitlementAsync(Guid productId, EntitlementRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null) return null;
        var key = BillingRules.NormalizeEntitlementKey(request.Key, product.Code)
            ?? throw new BusinessRuleException("Billing.EntitlementInvalid", $"Hak anahtarı \"{product.Code}.\" ile başlamalı; küçük harf, rakam ve tire içerebilir.");
        if (await db.Entitlements.AnyAsync(item => item.Key == key, cancellationToken))
            throw new BusinessRuleException("Billing.EntitlementExists", "Bu hak anahtarı zaten var.");
        db.Entitlements.Add(new Entitlement { Id = Guid.NewGuid(), ProductId = productId, Key = key, Name = Name(request.Name, "Hak adı"),
            HasLimit = request.HasLimit, CreatedAt = Now });
        await db.SaveChangesAsync(cancellationToken);
        return await ProductAsync(productId, cancellationToken);
    }

    /// <summary>The key is what product code checks; only the name and the limit flag change.</summary>
    public async Task<ProductView?> UpdateEntitlementAsync(Guid entitlementId, EntitlementRequest request, CancellationToken cancellationToken = default)
    {
        var entitlement = await db.Entitlements.SingleOrDefaultAsync(item => item.Id == entitlementId, cancellationToken);
        if (entitlement is null) return null;
        if (entitlement.HasLimit != request.HasLimit && await db.PlanEntitlements.AnyAsync(item => item.EntitlementId == entitlementId, cancellationToken))
            throw new BusinessRuleException("Billing.EntitlementInUse", "Bu hakkı veren plan varken sınır türü değiştirilemez.");
        entitlement.Name = Name(request.Name, "Hak adı");
        entitlement.HasLimit = request.HasLimit;
        await db.SaveChangesAsync(cancellationToken);
        return await ProductAsync(entitlement.ProductId, cancellationToken);
    }

    public async Task<bool> RemoveEntitlementAsync(Guid entitlementId, CancellationToken cancellationToken = default)
    {
        var entitlement = await db.Entitlements.SingleOrDefaultAsync(item => item.Id == entitlementId, cancellationToken);
        if (entitlement is null) return false;
        if (await db.PlanEntitlements.AnyAsync(item => item.EntitlementId == entitlementId, cancellationToken))
            throw new BusinessRuleException("Billing.EntitlementInUse", "Bu hakkı veren plan var; önce plandan çıkarın.");
        if (await db.EntitlementGrants.AnyAsync(item => item.EntitlementId == entitlementId, cancellationToken))
            throw new BusinessRuleException("Billing.EntitlementInUse", "Bu hak süren aboneliklerde kullanılıyor; silinemez.");
        db.Entitlements.Remove(entitlement);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Plans --------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<PlanView>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await db.Plans.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        return await ViewsAsync(plans, cancellationToken);
    }

    public async Task<PlanView> CreatePlanAsync(PlanRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var plan = new Plan { Id = Guid.NewGuid(), CreatedBy = actorId, CreatedAt = Now };
        var entitlements = await ApplyAsync(plan, request, cancellationToken);
        db.Plans.Add(plan);
        db.PlanEntitlements.AddRange(entitlements);
        await db.SaveChangesAsync(cancellationToken);
        return (await ViewsAsync([plan], cancellationToken))[0];
    }

    public async Task<PlanView?> UpdatePlanAsync(Guid id, PlanRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (plan is null) return null;
        var entitlements = await ApplyAsync(plan, request, cancellationToken);
        plan.UpdatedBy = actorId;
        plan.UpdatedAt = Now;
        db.PlanEntitlements.RemoveRange(await db.PlanEntitlements.Where(item => item.PlanId == id).ToListAsync(cancellationToken));
        db.PlanEntitlements.AddRange(entitlements);
        await db.SaveChangesAsync(cancellationToken);
        return (await ViewsAsync([plan], cancellationToken))[0];
    }

    private async Task<List<PlanEntitlement>> ApplyAsync(Plan plan, PlanRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.BuyerType)) throw new BusinessRuleException("Billing.PlanInvalid", "Alıcı türü geçersiz.");
        var problem = BillingRules.PlanProblem(request.BuyerType, request.Price, request.DurationDays, request.TrialDays, request.SeatCount,
            request.ExtraSeatPrice, request.IsContactOnly);
        if (problem is not null) throw new BusinessRuleException("Billing.PlanInvalid", problem);
        var features = (request.Features ?? []).Select(item => item?.Trim() ?? string.Empty).Where(item => item.Length > 0).ToArray();
        if (features.Length > MaxFeatures || features.Any(item => item.Length > MaxFeatureLength))
            throw new BusinessRuleException("Billing.PlanInvalid", "En çok 20 plan özelliği yazılabilir; her biri en fazla 200 karakter olabilir.");

        var requested = request.Entitlements ?? [];
        if (requested.Count == 0) throw new BusinessRuleException("Billing.PlanInvalid", "Plan en az bir hak vermelidir.");
        if (requested.Any(item => !Enum.IsDefined(item.Holder)) || requested.GroupBy(item => (item.EntitlementId, item.Holder)).Any(group => group.Count() > 1))
            throw new BusinessRuleException("Billing.PlanInvalid", "Aynı hak aynı kişiye iki kez verilemez.");
        var ids = requested.Select(item => item.EntitlementId).Distinct().ToArray();
        var known = await db.Entitlements.AsNoTracking().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (known.Count != ids.Length) throw new BusinessRuleException("Billing.PlanInvalid", "Seçilen haklardan biri bulunamadı.");
        foreach (var item in requested)
        {
            var entitlement = known[item.EntitlementId];
            if (item.Holder == EntitlementHolder.SeatHolder && request.SeatCount is null)
                throw new BusinessRuleException("Billing.PlanInvalid", "Koltuk sahibine hak vermek için planın koltuğu olmalıdır.");
            if (entitlement.HasLimit != item.LimitValue.HasValue || item.LimitValue is < 1 or > BillingRules.MaxSeats)
                throw new BusinessRuleException("Billing.PlanInvalid", entitlement.HasLimit
                    ? $"\"{entitlement.Name}\" hakkı için 1 ile 100.000 arasında bir sınır yazın."
                    : $"\"{entitlement.Name}\" hakkının sınırı yoktur.");
        }

        plan.Name = Name(request.Name, "Plan adı");
        plan.Description = (request.Description ?? string.Empty).Trim();
        if (plan.Description.Length > 2_000) throw new BusinessRuleException("Billing.PlanInvalid", "Plan açıklaması en fazla 2000 karakter olabilir.");
        plan.BuyerType = request.BuyerType;
        plan.Price = Math.Round(request.Price, 2, MidpointRounding.AwayFromZero);
        plan.Currency = BillingRules.DefaultCurrency;
        plan.DurationDays = request.DurationDays;
        plan.TrialDays = request.TrialDays;
        plan.SeatCount = request.SeatCount;
        plan.ExtraSeatPrice = request.ExtraSeatPrice is { } extra ? Math.Round(extra, 2, MidpointRounding.AwayFromZero) : null;
        plan.IsContactOnly = request.IsContactOnly;
        plan.IsActive = request.IsActive;
        plan.IsPublic = request.IsPublic;
        plan.SortOrder = request.SortOrder;
        plan.FeaturesJson = JsonSerializer.Serialize(features);
        return requested.Select(item => new PlanEntitlement { Id = Guid.NewGuid(), PlanId = plan.Id, EntitlementId = item.EntitlementId,
            Holder = item.Holder, LimitValue = item.LimitValue }).ToList();
    }

    private async Task<IReadOnlyList<PlanView>> ViewsAsync(IReadOnlyList<Plan> plans, CancellationToken cancellationToken)
    {
        var planIds = plans.Select(item => item.Id).ToArray();
        var rows = await (from link in db.PlanEntitlements.AsNoTracking()
                          join entitlement in db.Entitlements.AsNoTracking() on link.EntitlementId equals entitlement.Id
                          join product in db.Products.AsNoTracking() on entitlement.ProductId equals product.Id
                          where planIds.Contains(link.PlanId)
                          orderby product.SortOrder, entitlement.Key, link.Holder
                          select new { link.PlanId, View = new PlanEntitlementView(entitlement.Id, entitlement.Key, entitlement.Name, product.Code, link.Holder, link.LimitValue) })
            .ToListAsync(cancellationToken);
        var byPlan = rows.ToLookup(item => item.PlanId, item => item.View);
        return plans.Select(plan =>
        {
            var entitlements = byPlan[plan.Id].ToArray();
            return new PlanView(plan.Id, plan.Name, plan.Description, plan.BuyerType, plan.Price, plan.Currency, plan.DurationDays, plan.TrialDays,
                plan.SeatCount, plan.ExtraSeatPrice, plan.IsContactOnly, plan.IsActive, plan.IsPublic, plan.SortOrder,
                JsonSerializer.Deserialize<string[]>(plan.FeaturesJson) ?? [], entitlements,
                entitlements.Select(item => item.ProductCode).Distinct().ToArray());
        }).ToArray();
    }

    // ---- Seller -------------------------------------------------------------------------------------------------------

    public async Task<SellerView> GetSellerAsync(CancellationToken cancellationToken = default) =>
        View(await db.Sellers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == Seller.SingleId, cancellationToken) ?? new Seller());

    public async Task<SellerView> UpdateSellerAsync(SellerRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var email = Optional(request.Email, 320, "Satıcı e-postası");
        var alert = Optional(request.AlertEmail, 320, "Uyarı e-postası");
        if (email is not null && !email.Contains('@') || alert is not null && !alert.Contains('@'))
            throw new BusinessRuleException("Billing.SellerInvalid", "E-posta adresi geçersiz.");
        // Everything is checked before the row is touched, so a refused request leaves nothing behind.
        var title = Optional(request.Title, 200, "Unvan");
        var address = Optional(request.Address, 500, "Adres");
        var taxOffice = Optional(request.TaxOffice, 100, "Vergi dairesi");
        var taxNumber = Digits(request.TaxNumber, 10, 11, "Vergi numarası");
        var mersisNumber = Digits(request.MersisNumber, 16, 16, "MERSİS numarası");
        var phone = Optional(request.Phone, 40, "Telefon");
        var seller = await db.Sellers.SingleOrDefaultAsync(item => item.Id == Seller.SingleId, cancellationToken);
        if (seller is null)
        {
            seller = new Seller();
            db.Sellers.Add(seller);
        }
        seller.Title = title;
        seller.Address = address;
        seller.TaxOffice = taxOffice;
        seller.TaxNumber = taxNumber;
        seller.MersisNumber = mersisNumber;
        seller.Email = email;
        seller.Phone = phone;
        seller.AlertEmail = alert;
        seller.UpdatedBy = actorId;
        seller.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return View(seller);
    }

    // ---- Bank accounts ------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<BankAccountView>> GetBankAccountsAsync(CancellationToken cancellationToken = default) =>
        (await db.BankAccounts.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.BankName).ToListAsync(cancellationToken))
        .Select(View).ToArray();

    public async Task<BankAccountView> CreateBankAccountAsync(BankAccountRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var account = new BankAccount { Id = Guid.NewGuid(), CreatedBy = actorId, CreatedAt = Now };
        await ApplyAsync(account, request, cancellationToken);
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return View(account);
    }

    public async Task<BankAccountView?> UpdateBankAccountAsync(Guid id, BankAccountRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var account = await db.BankAccounts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (account is null) return null;
        await ApplyAsync(account, request, cancellationToken);
        account.UpdatedBy = actorId;
        account.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return View(account);
    }

    public async Task<bool> RemoveBankAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await db.BankAccounts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (account is null) return false;
        // Statements and their lines are financial records of this account; it can be closed, not removed.
        if (await db.BankStatementImports.AnyAsync(item => item.BankAccountId == id, cancellationToken))
            throw new BusinessRuleException("Billing.BankAccountInUse", "Bu hesaba ekstre yüklendi; silinemez. Ödeme sayfasından kaldırmak için hesabı kapatın.");
        db.BankAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ApplyAsync(BankAccount account, BankAccountRequest request, CancellationToken cancellationToken)
    {
        var iban = BillingRules.NormalizeIban(request.Iban)
            ?? throw new BusinessRuleException("Billing.BankAccountInvalid", "IBAN geçersiz; TR ile başlayan 26 karakterlik IBAN girin.");
        if (await db.BankAccounts.AnyAsync(item => item.Iban == iban && item.Id != account.Id, cancellationToken))
            throw new BusinessRuleException("Billing.BankAccountExists", "Bu IBAN başka bir banka hesabında kayıtlı.");
        account.BankName = Name(request.BankName, "Banka adı");
        account.AccountHolder = Name(request.AccountHolder, "Hesap sahibi");
        account.Iban = iban;
        account.Instructions = Optional(request.Instructions, 2_000, "Açıklama");
        account.StatementTemplate = Optional(request.StatementTemplate, 40, "Döküm şablonu");
        account.IsActive = request.IsActive;
        account.SortOrder = request.SortOrder;
    }

    // ---- Public catalog -----------------------------------------------------------------------------------------------

    public async Task<PublicCatalog?> GetPublicCatalogAsync(string productCode, CancellationToken cancellationToken = default)
    {
        var code = BillingRules.NormalizeCode(productCode);
        if (code is null) return null;
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken);
        if (product is null) return null;
        // A plan belongs to a product when it gives one of the product's entitlements; a bundle shows under each of its products.
        var planIds = await (from link in db.PlanEntitlements.AsNoTracking()
                             join entitlement in db.Entitlements.AsNoTracking() on link.EntitlementId equals entitlement.Id
                             where entitlement.ProductId == product.Id
                             select link.PlanId).Distinct().ToListAsync(cancellationToken);
        var plans = await db.Plans.AsNoTracking().Where(item => planIds.Contains(item.Id) && item.IsActive && item.IsPublic)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var accounts = await db.BankAccounts.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.BankName).ToListAsync(cancellationToken);
        var seller = await db.Sellers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == Seller.SingleId, cancellationToken);
        return new PublicCatalog(product.Code, product.Name, product.RequireSubscription, seller is { IsComplete: true } && accounts.Count > 0,
            await ViewsAsync(plans, cancellationToken),
            accounts.Select(item => new PublicBankAccount(item.BankName, item.AccountHolder, item.Iban, BillingRules.FormatIban(item.Iban), item.Instructions)).ToArray());
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------------

    private async Task<ProductView?> ProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return null;
        var entitlements = await db.Entitlements.AsNoTracking().Where(item => item.ProductId == id).OrderBy(item => item.Key).ToListAsync(cancellationToken);
        return View(product, entitlements);
    }

    private static ProductView View(Product product, IReadOnlyList<Entitlement> entitlements) =>
        new(product.Id, product.Code, product.Name, product.RequireSubscription, product.BankTransferApproval, product.IsActive, product.SortOrder,
            entitlements.Where(item => item.ProductId == product.Id).Select(item => new EntitlementView(item.Id, item.Key, item.Name, item.HasLimit)).ToArray());

    private static SellerView View(Seller seller) =>
        new(seller.Title, seller.Address, seller.TaxOffice, seller.TaxNumber, seller.MersisNumber, seller.Email, seller.Phone, seller.AlertEmail, seller.IsComplete);

    private static BankAccountView View(BankAccount account) =>
        new(account.Id, account.BankName, account.AccountHolder, account.Iban, BillingRules.FormatIban(account.Iban), account.Instructions,
            account.StatementTemplate, account.IsActive, account.SortOrder);

    private static string Name(string? value, string label)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > 200
            ? throw new BusinessRuleException("Billing.Invalid", $"{label} 1–200 karakter olmalıdır.")
            : text;
    }

    private static string? Optional(string? value, int maxLength, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length > maxLength ? throw new BusinessRuleException("Billing.Invalid", $"{label} en fazla {maxLength} karakter olabilir.") : text;
    }

    private static string? Digits(string? value, int min, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length < min || text.Length > max || !text.All(char.IsAsciiDigit)
            ? throw new BusinessRuleException("Billing.SellerInvalid", min == max ? $"{label} {min} haneli olmalıdır." : $"{label} {min} ya da {max} haneli olmalıdır.")
            : text;
    }
}
