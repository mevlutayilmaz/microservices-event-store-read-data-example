﻿using EventStore.Client;
using MongoDB.Driver;
using Shared.Events;
using Shared.Services.Abstractions;
using System.Reflection;
using System.Text.Json;

namespace Product.Event.Handler.Service.Services
{
    public class EventStoreBackgroundService(IEventStoreService eventStoreService, IMongoDBService mongoDBService) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await eventStoreService.SubscribeToStreamAsync(
                "products-stream",
                async (ss, re, ct) =>
                {
                    string eventType = re.Event.EventType;
                    var @event = JsonSerializer.Deserialize(re.Event.Data.ToArray(), Assembly.Load("Shared").GetTypes().FirstOrDefault(t => t.Name == eventType));

                    var productCollection = mongoDBService.GetCollection<Shared.Models.Product>("Products");
                    
                    Shared.Models.Product product = null;
                    switch (@event)
                    {
                        case NewProductAddedEvent e:
                            bool hasProduct = await (await productCollection.FindAsync(p => p.Id == e.ProductId)).AnyAsync();
                            if (!hasProduct)
                                await productCollection.InsertOneAsync(new()
                                {
                                    Id = e.ProductId,
                                    Name = e.ProductName,
                                    Count = e.InitialCount,
                                    IsAvailable = e.IsAvailable,
                                    Price = e.InitialPrice,
                                });
                            break;
                        case CountDecreasedEvent e:
                            product = await(await productCollection.FindAsync(p => p.Id == e.ProductId)).FirstOrDefaultAsync();
                            if(product != null)
                            {
                                product.Count -= e.DecrementAmount;
                                await productCollection.FindOneAndReplaceAsync(p => p.Id == e.ProductId, product);
                            }
                            break;
                        case CountIncreasedEvent e:
                            product = await (await productCollection.FindAsync(p => p.Id == e.ProductId)).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                product.Count += e.IncrementAmount;
                                await productCollection.FindOneAndReplaceAsync(p => p.Id == e.ProductId, product);
                            }
                            break;
                        case PriceDecreasedEvent e:
                            product = await (await productCollection.FindAsync(p => p.Id == e.ProductId)).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                product.Price -= e.DecrementAmount;
                                await productCollection.FindOneAndReplaceAsync(p => p.Id == e.ProductId, product);
                            }
                            break;
                        case PriceIncreasedEvent e:
                            product = await (await productCollection.FindAsync(p => p.Id == e.ProductId)).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                product.Price += e.IncrementAmount;
                                await productCollection.FindOneAndReplaceAsync(p => p.Id == e.ProductId, product);
                            }
                            break;
                        case AvailabilityChangedEvent e:
                            product = await (await productCollection.FindAsync(p => p.Id == e.ProductId)).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                product.IsAvailable = e.IsAvailable;
                                await productCollection.FindOneAndReplaceAsync(p => p.Id == e.ProductId, product);
                            }
                            break;

                    }
                });
        }
    }
}