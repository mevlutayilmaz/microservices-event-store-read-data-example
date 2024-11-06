using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Product.Application.Models.ViewModels;
using Shared.Events;
using Shared.Services.Abstractions;

namespace Product.Application.Controllers
{
    public class ProductController(IEventStoreService eventStoreService, IMongoDBService mongoDBService) : Controller
    {
        IMongoCollection<Shared.Models.Product> ProductCollection { get => mongoDBService.GetCollection<Shared.Models.Product>("Products"); }
        async Task<Shared.Models.Product> GetProductAsync(string productId) => await (await ProductCollection.FindAsync(p => p.Id == productId)).FirstOrDefaultAsync();

        public async Task<IActionResult> Index()
        {
            var products = await (await ProductCollection.FindAsync(t => true)).ToListAsync();
            return View(products);
        }

        public IActionResult CreateProduct()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> CreateProduct(CreateProductVM model) 
        {
            NewProductAddedEvent newProductAddedEvent = new()
            {
                ProductId = Guid.NewGuid().ToString() ,
                ProductName = model.Name,
                InitialCount = model.Count,
                InitialPrice = model.Price,
                IsAvailable = model.IsAvailable,
            };
            await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(newProductAddedEvent)]);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(string productId)
        {
            var product = await (await ProductCollection.FindAsync(p => p.Id == productId)).FirstOrDefaultAsync();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> CountUpdate(Shared.Models.Product model)
        {
            var product = await GetProductAsync(model.Id);

            if (product.Count > model.Count)
            {
                CountDecreasedEvent countDecreasedEvent = new()
                {
                    ProductId = model.Id,
                    DecrementAmount = product.Count - model.Count,
                };
                await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(countDecreasedEvent)]);
            }
            else if (product.Count < model.Count)
            {
                CountIncreasedEvent countIncreasedEvent = new()
                {
                    ProductId = model.Id,
                    IncrementAmount = model.Count - product.Count,
                };
                await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(countIncreasedEvent)]);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> PriceUpdate(Shared.Models.Product model)
        {
            var product = await GetProductAsync(model.Id);

            if (product.Price > model.Price)
            {
                PriceDecreasedEvent priceDecreasedEvent = new()
                {
                    ProductId = model.Id,
                    DecrementAmount = product.Price - model.Price
                };
                await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(priceDecreasedEvent)]);
            }
            else if (product.Price < model.Price)
            {
                PriceIncreasedEvent priceIncreasedEvent = new()
                {
                    ProductId = model.Id,
                    IncrementAmount = model.Price - product.Price
                };
                await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(priceIncreasedEvent)]);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AvailableUpdate(Shared.Models.Product model)
        {
            var product = await GetProductAsync(model.Id);

            if (product.IsAvailable != model.IsAvailable)
            {
                AvailabilityChangedEvent availabilityChangedEvent = new()
                {
                    ProductId = model.Id,
                    IsAvailable = model.IsAvailable,
                };
                await eventStoreService.AppendToStreamAsync("products-stream", [eventStoreService.GenerateEventData(availabilityChangedEvent)]);
            }
            return RedirectToAction("Index");
        }
    }
}
