using Nest;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace IndexSqlData
{
    class Program
    {
        static ConnectionSettings settings;
        static ElasticClient client;

        static void Main(string[] args)
        {
            initElasticConnection();

            FilterNestedObject();
            //GetPriceMax();
            Console.ReadLine();
        }

        #region Filter Nested Object

        static void FilterNestedObject()
        {
            var res = client.Search<ProductUserLike>(s => s
                .Source(sel => sel.Includes
                        (
                            f => f.Field("ProductName")
                        )
                    )
                .Query(q => q
                    .Nested(c => c
                        .InnerHits(ih => ih.Source(sf => sf.Includes(ul => ul.Field("UserLiked.UserId"))))
                        .Path(p => p.UserLiked)
                        .Query(nq => nq
                            .Bool(b => b.Must(mt => mt.Term(p => p.UserLiked[0].UserID, 3)))
                        )
                    )
                )
            );

            //Console.WriteLine(res.DebugInformation);

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                Console.Write("Product name: ");
                Console.WriteLine(results.Current.Source.ProductName);

                // print user ID
                var users = results.Current.InnerHits["UserLiked"].Documents<UserLiked>().GetEnumerator();
                if (users.MoveNext())
                {
                    Console.Write($"User ID: { users.Current.UserID }");
                }
            }
        }

        #endregion Filter Nested Object

        #region Aggs

        static void GetPriceAvg()
        {
            var avg = new SearchRequest<Product>
            {
                Aggregations = new AggregationDictionary
                {
                    {
                        "avg", new AverageAggregation("avg_price", new Field("price"))
                    }
                }
            };

            var result = client.SearchAsync<Product>(avg).Result;

            Console.WriteLine(result.DebugInformation);

            var avg_price = result.Aggregations.Average("avg");
            Console.WriteLine("Price Average: {0}", avg_price.Value);
        }

        static void GetPriceMax()
        {
            var res = client.Search<Product>(s => s
                .Aggregations(max => max.Max("price_max", f => f.Field(p => p.Price)))
            );

            var price_max = res.Aggregations.Average("price_max");
            Console.WriteLine("Price MAX: {0}", price_max.Value);
        }

        static void GetPriceStats()
        {
            var res = client.Search<Product>(s => s
                .Aggregations(stats => stats.Stats("stats_price", f => f.Field(p => p.Price)))
            );

            var stats_price = res.Aggregations.Stats("stats_price");
            Console.WriteLine("Price MAX: {0}", stats_price.Max);
        }

        #endregion Aggs

        #region Bool Query

        /// <summary>
        /// bool query - must
        /// </summary>
        static void ReadBoolMustQuery()
        {
            var res = client.Search<Product>(s => s
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            bs => bs.Term(p => p.CategoryID, 1)
                        )
                    )
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                Console.WriteLine(results.Current.Source.Price);
            }
        }

        /// <summary>
        /// bool query - must
        /// </summary>
        static void ReadBoolMust_MustNotQuery()
        {
            var res = client.Search<Product>(s => s
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            bs => bs.Term(p => p.CategoryID, 1)
                        )
                        .MustNot(
                            bs => bs.Term(p => p.Name, "تفاح")
                        )
                    )
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                Console.WriteLine(results.Current.Source.Price);
            }
        }

        #endregion Bool Query

        #region Read data from _source
        static void ReadWithSource()
        {
            var res = client.Search<Product>(s => s
                .Query(q => q
                    .MatchAll()
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();
            while (results.MoveNext())
            {
                Console.WriteLine(results.Current.Source.Price);
            }
        }

        static void ReadWithoutSource()
        {
            var res = client.Search<Product>(s => s
                .Source(false)
                .Query(q => q
                    .MatchAll()
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                if (results.Current.Source != null)
                    Console.WriteLine(results.Current.Source.Price);
            }
        }

        static void ReadWithFilteredSource()
        {
            var res = client.Search<Product>(s => s
                .Source(sel => sel.Includes
                    (
                        f => f.Field("price").Field("name")
                    )
                )
                .Query(q => q
                    .MatchAll()
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                Console.WriteLine(results.Current.Source.Price);
            }
        }

        #endregion Read data from _source

        #region Read from store

        static void ReadStoredField()
        {
            var res = client.Search<Product>(s => s
                .StoredFields(f => f.Field("price"))
                .Query(q => q
                    .MatchAll()
                )
            );

            Console.WriteLine("Total:" + res.Total.ToString());
            var results = res.Hits.GetEnumerator();

            while (results.MoveNext())
            {
                Console.WriteLine(results.Current.Fields.ValueOf<Product, int>(p => p.Price));
            }
        }

        #endregion Read from store

        #region Indexing Data

        private static void initElasticConnection()
        {
            settings = new ConnectionSettings(new Uri("http://localhost:9200"));
            settings.BasicAuthentication("elastic", "P@ssw0rd");

            // set index name for Product type to products
            settings.DefaultMappingFor<Product>(p => p.IndexName("products"));

            // set index name for Category type to categories
            settings.DefaultMappingFor<Category>(p => p.IndexName("categories"));

            // set index name for Article type to articles
            settings.DefaultMappingFor<Article>(p => p.IndexName("articles"));

            // set index name for Product type to liked products
            settings.DefaultMappingFor<ProductUserLike>(p => p.IndexName("elastic_nested"));

            settings.DefaultFieldNameInferrer(p => p.ToLower());
            settings.DisableDirectStreaming();

            client = new ElasticClient(settings);
        }

        private static void UpdateElasticsearchIndex()
        {
            int lastIndexedID = 0;
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand($"select ID, EntityID from UpdatedEntities order by ID asc", conn))
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Product item = GetProduct(Convert.ToInt32(r["EntityID"]));
                            client.IndexDocument(item);

                            lastIndexedID = Convert.ToInt32(r["ID"]);
                        }
                    }

                    // update last indexed ID
                    cmd.CommandText = $"delete from UpdatedEntities where ID <= {lastIndexedID}";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static Product GetProduct(int id)
        {
            Product product = null;
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand($"select * from Products where ID = {id}", conn))
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            product = new Product()
                            {
                                ID = Convert.ToInt32(r["ID"]),
                                Name = r["Name"].ToString(),
                                Price = Convert.ToInt32(r["Price"]),
                                CategoryID = Convert.ToInt32(r["CategoryID"]),
                                LastUpdateDateTime = Convert.ToDateTime(r["LastUpdateDateTime"]),
                                CategoryName = r["CategoryName"].ToString()
                            };

                        }
                    }
                }
            }

            return product;
        }

        private static int GetLastIndexedID()
        {
            int lastIndexedID = 0;
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand("select LastIndexedID from IndexedID", conn))
                {
                    conn.Open();
                    object id = cmd.ExecuteScalar();
                    lastIndexedID = Convert.ToInt32(id);
                }
            }

            return lastIndexedID;
        }

        private static void indexAllData()
        {
            indexCategories();
            indexProducts();
            indexArticles();
        }

        private static List<Product> GetProducts()
        {
            List<Product> items = new List<Product>();
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand("select * from Products", conn))
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Product item = new Product()
                            {
                                ID = Convert.ToInt32(r["ID"]),
                                Name = r["Name"].ToString(),
                                Price = Convert.ToInt32(r["Price"]),
                                CategoryID = r["CategoryID"] == DBNull.Value ? null : new Nullable<int>(Convert.ToInt32(r["CategoryID"])),
                                LastUpdateDateTime = Convert.ToDateTime(r["LastUpdateDateTime"]),
                                CategoryName = r["CategoryName"].ToString()
                            };

                            items.Add(item);
                        }
                    }
                }
            }

            return items;
        }

        private static List<Article> GetArticles()
        {
            List<Article> items = new List<Article>();
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand("select * from Articles", conn))
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Article item = new Article()
                            {
                                ID = Convert.ToInt32(r["ID"]),
                                Title = r["Title"].ToString(),
                                Body = r["Body"].ToString()
                            };

                            items.Add(item);
                        }
                    }
                }
            }

            return items;
        }

        private static List<Category> GetCategories()
        {
            List<Category> items = new List<Category>();
            using (SqlConnection conn = new SqlConnection(@"Server=MAIS\SQL2019;Database=IndexSqlData;User ID=sa;Password=1"))
            {
                using (SqlCommand cmd = new SqlCommand("select * from Categories", conn))
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Category item = new Category()
                            {
                                ID = Convert.ToInt32(r["ID"]),
                                Name = r["Name"].ToString()
                            };

                            items.Add(item);
                        }
                    }
                }
            }

            return items;
        }

        private static void indexProducts2()
        {
            List<Product> items = GetProducts();
            foreach (var item in items)
            {
                var indexResponse = client.IndexDocument(item);

                if (indexResponse.IsValid)
                {
                    Console.WriteLine($"Product {item.ID}, Done!");
                }
                else
                {
                    Console.WriteLine(indexResponse.OriginalException);
                }
            }

            Console.ReadLine();
        }

        private static void indexProducts()
        {
            List<Product> items = GetProducts();
            foreach (var item in items)
            {
                var indexResponse = client.IndexDocument(item);

                if (indexResponse.IsValid)
                {
                    Console.WriteLine($"Product {item.ID}, Done!");
                }
                else
                {
                    Console.WriteLine(indexResponse.OriginalException);
                }
            }
        }

        private static void indexArticles()
        {
            List<Article> items = GetArticles();
            foreach (var item in items)
            {
                var indexResponse = client.IndexDocument(item);

                if (indexResponse.IsValid)
                {
                    Console.WriteLine($"Article {item.ID}, Done!");
                }
                else
                {
                    Console.WriteLine(indexResponse.OriginalException);
                }
            }
        }

        private static void indexCategories()
        {
            List<Category> items = GetCategories();
            foreach (var item in items)
            {
                var indexResponse = client.IndexDocument(item);

                if (indexResponse.IsValid)
                {
                    Console.WriteLine($"Category {item.ID}, Done!");
                }
                else
                {
                    Console.WriteLine(indexResponse.OriginalException);
                }
            }
        }

        #endregion Indexing Data
    }
}
