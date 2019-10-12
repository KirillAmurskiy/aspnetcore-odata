using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Query;

namespace AspNetCore.OData.Sum
{
    public static class ODataExtensions
    {
        public static bool IsSum<T>(this ODataQueryOptions<T> options)
        {
            return options.Apply != null
                   && options.Apply.RawValue.Contains("with sum as");
        }

        public static async Task<IQueryable<T>> ODataSumAsync<T>(
            this IQueryable<T> data,
            ODataQueryOptions<T> options)
            where T: new()
        {
            var sum = new ODataSum<T>();

            return await sum.Sum(options, data);
        }
    }
}