using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Web.Components
{
    public partial class ProductReviewViewComponent : NopViewComponent
    {
        private readonly IAclService _aclService;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly CatalogSettings _catalogSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;


        public ProductReviewViewComponent(IAclService aclService,
            IProductModelFactory productModelFactory,
            IProductService productService,
            IStoreMappingService storeMappingService,
            IWorkContext workContext,
            ICustomerService customerService,
            CatalogSettings catalogSettings,
            ILocalizationService localizationService,
            IOrderService orderService)
        {
            _aclService = aclService;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _storeMappingService = storeMappingService;
            _workContext = workContext;
            _customerService = customerService;
            _catalogSettings = catalogSettings;
            _localizationService = localizationService;
            _orderService = orderService;
        }

        public async Task<IViewComponentResult> InvokeAsync(int productId)
        {
            var product = await _productService.GetProductByIdAsync(productId);
            if (product == null || product.Deleted || !product.Published || !product.AllowCustomerReviews)
                return Content(string.Empty);

            var model = new ProductReviewsModel();
            model = await _productModelFactory.PrepareProductReviewsModelAsync(model, product);

            //await ValidateProductReviewAvailabilityAsync(product);

            //default value
            model.AddProductReview.Rating = _catalogSettings.DefaultProductRatingValue;

            //default value for all additional review types
            if (model.ReviewTypeList.Count > 0)
                foreach (var additionalProductReview in model.AddAdditionalProductReviewList)
                {
                    additionalProductReview.Rating = additionalProductReview.IsRequired ? _catalogSettings.DefaultProductRatingValue : 0;
                }

            return View("~/Plugins/SevenSpikes.Nop.Plugins.NopQuickTabs/Themes/Venture/Views/Components/ProductReviewsTab/ProductReviewsTab.cshtml", model);
        }

        protected virtual async Task ValidateProductReviewAvailabilityAsync(Product product)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            if (await _customerService.IsGuestAsync(customer) && !_catalogSettings.AllowAnonymousUsersToReviewProduct)
                ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Reviews.OnlyRegisteredUsersCanWriteReviews"));

            if (!_catalogSettings.ProductReviewPossibleOnlyAfterPurchasing)
                return;

            var hasCompletedOrders = product.ProductType == ProductType.SimpleProduct
                ? await HasCompletedOrdersAsync(product)
                : await (await _productService.GetAssociatedProductsAsync(product.Id)).AnyAwaitAsync(HasCompletedOrdersAsync);

            if (!hasCompletedOrders)
                ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Reviews.ProductReviewPossibleOnlyAfterPurchasing"));
        }

        protected virtual async ValueTask<bool> HasCompletedOrdersAsync(Product product)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            return (await _orderService.SearchOrdersAsync(customerId: customer.Id,
                productId: product.Id,
                osIds: new List<int> { (int)OrderStatus.Complete },
                pageSize: 1)).Any();
        }
    }
}