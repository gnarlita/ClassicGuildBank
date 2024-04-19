using ClassicGuildBankData.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WowheadItemSeeder.WowheadSchema;
using CGB = ClassicGuildBankData.Models;

namespace WowheadItemSeeder {
    class Program {
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args) {
            bool fullInitialize = args.Any(a => a == "--full");
            bool update = args.Any(a => a == "--update");
            bool itemLanguage = args.Any(a => a == "--lang");

            Console.WriteLine("Wowhead Item Initializer");

            if ( fullInitialize )
                Console.WriteLine("Full item initialization from Wowhead");
            else if ( update )
                Console.WriteLine("Updating item values");
            else if ( itemLanguage )
                Console.WriteLine("Updating Locale Names for Items");
            else {
                Console.WriteLine("No Command Parameter Specified");
                Environment.Exit(-1);
            }


            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile("appsettings.json");
            var config = configBuilder.Build();

            var bankDb1 = new ClassicGuildBankDbContext(config);
            var bankDb2 = new ClassicGuildBankDbContext(config);
            var bankDb3 = new ClassicGuildBankDbContext(config);
            var bankDb4 = new ClassicGuildBankDbContext(config);
            var bankDb5 = new ClassicGuildBankDbContext(config);

            if ( fullInitialize )
                await DoFullInitialize(bankDb1, bankDb2, bankDb3, bankDb4, bankDb5);
            else if ( update )
                await DoUpdate(bankDb1);
            else if ( itemLanguage )
                await DoLanguageUpdate(bankDb1);
        }

        public static async Task DoFullInitialize(ClassicGuildBankDbContext bankDb1, ClassicGuildBankDbContext bankDb2, ClassicGuildBankDbContext bankDb3, ClassicGuildBankDbContext bankDb4, ClassicGuildBankDbContext bankDb5) {
            var deserializer = new XmlSerializer(typeof(Wowhead));

            int itemCnt = 0;
            int maxId = bankDb1.Items.Any() ? bankDb1.Items.Max(i => i.Id) : 0;

            var conn1 = bankDb1.Database.GetDbConnection();
            if ( conn1.State != System.Data.ConnectionState.Open)
                conn1.Open();

            var conn2 = bankDb2.Database.GetDbConnection();
            if ( conn2.State != System.Data.ConnectionState.Open )
                conn2.Open();

            var conn3 = bankDb3.Database.GetDbConnection();
            if ( conn3.State != System.Data.ConnectionState.Open )
                conn3.Open();

            var conn4 = bankDb4.Database.GetDbConnection();
            if ( conn4.State != System.Data.ConnectionState.Open )
                conn4.Open();

            var conn5 = bankDb5.Database.GetDbConnection();
            if ( conn5.State != System.Data.ConnectionState.Open )
                conn5.Open();

            await bankDb1.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Item ON;");
            await bankDb2.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Item ON;");
            await bankDb3.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Item ON;");
            await bankDb4.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Item ON;");
            await bankDb5.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Item ON;");

            if ( maxId < 25000 ) {
                itemCnt += await DoImportItems(maxId, 24360, bankDb1, bankDb2, bankDb3, bankDb4, bankDb5, deserializer);
                maxId = 24360;
            }

            if ( maxId < 225000 ) {
                itemCnt += await DoImportItems(Math.Max(184937, maxId), 225000, bankDb1, bankDb2, bankDb3, bankDb4, bankDb5, deserializer);
                maxId = 225000;
            }

            itemCnt += await bankDb1.SaveChangesAsync();
            itemCnt += await bankDb2.SaveChangesAsync();
            itemCnt += await bankDb3.SaveChangesAsync();
            itemCnt += await bankDb4.SaveChangesAsync();
            itemCnt += await bankDb5.SaveChangesAsync();

            conn1.Close();
            conn2.Close();
            conn3.Close();
            conn4.Close();
            conn5.Close();

            Console.WriteLine($"Imported {itemCnt} Records");
            Console.ReadLine();
        }

        private static async Task<int> DoImportItems(int minId, int maxId, ClassicGuildBankDbContext bankDb1, ClassicGuildBankDbContext bankDb2, ClassicGuildBankDbContext bankDb3, ClassicGuildBankDbContext bankDb4, ClassicGuildBankDbContext bankDb5, XmlSerializer deserializer) {
            var itemCnt = 0;

            for ( var itemId = minId; itemId <= maxId; itemId += 5 ) {
                Task.WaitAll(
                    DoItemImport(bankDb1, deserializer, itemId),
                    DoItemImport(bankDb2, deserializer, itemId + 1),
                    DoItemImport(bankDb3, deserializer, itemId + 2),
                    DoItemImport(bankDb4, deserializer, itemId + 3),
                    DoItemImport(bankDb5, deserializer, itemId + 4)
                );

                if ( itemId % 100 == 0 ) {
                    itemCnt += await bankDb1.SaveChangesAsync();
                    itemCnt += await bankDb2.SaveChangesAsync();
                    itemCnt += await bankDb3.SaveChangesAsync();
                    itemCnt += await bankDb4.SaveChangesAsync();
                    itemCnt += await bankDb5.SaveChangesAsync();
                }
            }

            return itemCnt;
        }

        private static async Task<bool> DoItemImport(ClassicGuildBankDbContext bankDb, XmlSerializer deserializer, int itemId) {
            var response = await httpClient.GetAsync($"https://wowhead.com/classic/item={itemId}?xml");
            var xmlStream = await response.Content.ReadAsStreamAsync();

            var wItem = (Wowhead)deserializer.Deserialize(xmlStream);

            if ( wItem.Item == null ) {
                Console.WriteLine($"Failed to locate item {itemId}");
                return false;
            }

            Console.WriteLine($"Converting Item {itemId}");

            var dbItem = bankDb.Items.FirstOrDefault(i => i.Id == Convert.ToInt32(wItem.Item.Id));

            if ( dbItem != null ) {
                return false;
            }

            dbItem = new CGB.Item {
                Id = Convert.ToInt32(wItem.Item.Id),
                Name = wItem.Item.Name,
                Quality = wItem.Item.Quality.Text,
                Icon = wItem.Item.Icon.Text,
                Class = Convert.ToInt32(wItem.Item.Class.Id),
                Subclass = Convert.ToInt32(wItem.Item.Subclass.Id)
            };

            bankDb.Items.Add(dbItem);

            return true;
        }

        public static async Task DoUpdate(ClassicGuildBankDbContext bankDb) {
            var itemCnt = bankDb.Items.Count();
            var batchCnt = (itemCnt / 1000) + 1;
            var deserializer = new XmlSerializer(typeof(Wowhead));

            for ( int i = 0; i < batchCnt; i++ ) {
                foreach ( var item in bankDb.Items.Skip(i * 1000).Take(1000).ToList() ) {
                    var result = await httpClient.GetAsync($"https://wowhead.com/classic/item={item.Id}?xml");
                    var xmlStream = await result.Content.ReadAsStreamAsync();
                    var str = await result.Content.ReadAsStringAsync();

                    var wItem = (Wowhead)deserializer.Deserialize(xmlStream);

                    if ( wItem.Item == null ) {
                        Console.WriteLine($"Failed to locate item {item.Id} - {item.Name}");
                        continue;
                    }

                    Console.WriteLine($"Updating Item {item.Id} - {item.Name}");

                    bankDb.Items.Attach(item);
                    item.Class = Convert.ToInt32(wItem.Item.Class.Id);
                    item.Subclass = Convert.ToInt32(wItem.Item.Subclass.Id);
                }
                await bankDb.SaveChangesAsync();
            }

            Console.WriteLine("Finished Updating Items");
            Console.ReadLine();
        }

        public static async Task DoLanguageUpdate(ClassicGuildBankDbContext bankDb) {
            var minId = bankDb.Items.Where(i => String.IsNullOrEmpty(i.PtName)).Min(i => i.Id);
            var itemCnt = bankDb.Items.Where(i => i.Id >= minId).Count();
            var batchCnt = (itemCnt / 1000) + 1;
            var deserializer = new XmlSerializer(typeof(Wowhead));

            var languages = new[] { /*"ru", "de", "fr", "it", "es", "cn",*/ "ko", "pt" };

            for ( int i = 0; i < batchCnt; i++ ) {
                foreach ( var item in bankDb.Items.Where(item => item.Id >= minId).Skip(i * 1000).Take(1000).ToList() ) {
                    bankDb.Items.Attach(item);
                    foreach ( var lan in languages ) {
                        var result = await httpClient.GetAsync($"https://{lan}.wowhead.com/classic/item={item.Id}?xml");
                        var byteArray = await result.Content.ReadAsByteArrayAsync();
                        var str = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);
                        var reader = new StringReader(str);
                        //var str = await result.Content.ReadAsStringAsync();
                        string localeName = String.Empty;
                        try {
                            var wItem = (Wowhead)deserializer.Deserialize(reader);
                            localeName = wItem.Item.Name;
                        }
                        catch {
                            var j = str.IndexOf("<name><![CDATA[") + 15;
                            var k = str.IndexOf("]]></name>", i);
                            if ( j > 0 && k > 0 )
                                localeName = str.Substring(j, k - j);
                        }

                        if ( string.IsNullOrEmpty(localeName) ) {
                            Console.WriteLine($"Failed to locate item {item.Id} - {item.Name}");
                            continue;
                        }

                        Console.WriteLine($"Updating Item {item.Id} - {item.Name} for Locale: {lan}");

                        switch ( lan ) {
                        case "ru":
                            item.RuName = localeName;
                            break;
                        case "de":
                            item.DeName = localeName;
                            break;
                        case "fr":
                            item.FrName = localeName;
                            break;
                        case "it":
                            item.ItName = localeName;
                            break;
                        case "cn":
                            item.CnName = localeName;
                            break;
                        case "es":
                            item.EsName = localeName;
                            break;
                        case "ko":
                            item.KoName = localeName;
                            break;
                        case "pt":
                            item.PtName = localeName;
                            break;
                        }
                    }
                }
                await bankDb.SaveChangesAsync();
            }

            Console.WriteLine("Finished Updating Item Locale Names");
            Console.ReadLine();
        }
    }
}
