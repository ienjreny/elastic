using Nest;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndexSqlData
{
    [ElasticsearchType(IdProperty = "ID")]
    public class Product
    {
        [Ignore]
        public int ID { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }

        public int? CategoryID { get; set; }

        [Date(Name = "timestampe")]
        public DateTime LastUpdateDateTime { get; set; }

        public string CategoryName { get; set; }

    }
}
