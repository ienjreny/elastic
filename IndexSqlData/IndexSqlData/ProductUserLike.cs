using Nest;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndexSqlData
{
    public class ProductUserLike
    {
        [Text(Name = "ProductName")]
        public string ProductName { get; set; }

        [Nested(Name = "UserLiked")]
        public List<UserLiked> UserLiked { get; set; }
    }

    public class UserLiked
    {
        [IntegerRange(Name = "UserId")]
        public int UserID { get; set; }
    }
}
