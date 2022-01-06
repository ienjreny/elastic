using Nest;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndexSqlData
{
    [ElasticsearchType(IdProperty = "ID")]
    public class Category
    {
        [Ignore]
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
