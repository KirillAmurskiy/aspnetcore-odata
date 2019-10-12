using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.OData.Sum
{
    public class ODataSum<T>
        where T: new()
    {
        public async Task<IQueryable<T>> Sum(
            ODataQueryOptions<T> options,
            IQueryable<T> data)
        {
            var queryStr = ExtractQueryString(options);
            var filterValue = ExtractFilter(queryStr);
            var propertyName = ExtractPropertyName(queryStr);
            
            using (var context = new SumContext(options, filterValue))
            {
                var opt = context.GetOptions();
                
                // apply filtration to data
                var query = (IQueryable<T>) opt.ApplyTo(data);

                // get stub for hacking odata middleware
                var stub = await query.FirstOrDefaultAsync();
                if (stub == null)
                {
                    stub = new T();
                }
                
                // execute sum
                var sum = await EntityFrameworkQueryableExtensions.SumAsync(query, GetSumExpression(propertyName));

                // update stub
                GetUpdateStubFunc(propertyName, sum)(stub);
            
                return new List<T> {stub}
                    .AsQueryable();
            }
        }

        private string ExtractQueryString(ODataQueryOptions<T> options)
        {
            return Uri.UnescapeDataString(options.Request.QueryString.ToString()).Replace('+', ' ');
        }
        
        private string ExtractFilter(string query)
        {
            var filterValue = string.Empty;
            var filterRegex = new Regex(@"filter\((.*?)\)/");
            var match = filterRegex.Match(query);
            if (match.Success)
            {
                filterValue = match.Groups[1].Value;
            }

            return filterValue;
        }

        private string ExtractPropertyName(string query)
        {
            var propertyName = string.Empty;
            var regex = new Regex(@"aggregate\((.*?) with sum as (.*?)\)");
            var match = regex.Match(query);
            if (match.Success)
            {
                propertyName = match.Groups[1].Value;
            }

            return propertyName;
        }
        
        private static dynamic GetSumExpression(string propertyName)
        {
            //x => x.propertyName
            var param = Expression.Parameter(typeof(T), "x");

            var pInfo = typeof(T).GetProperty(propertyName);
            Expression body = Expression.Property(param, pInfo);
            
            return Expression.Lambda(body: body, parameters: param);
        }
        
        private static Action<T> GetUpdateStubFunc(string propertyName, object value)
        {
            //x => x.propertyName = value
            var param = Expression.Parameter(typeof(T), "x");

            var pInfo = typeof(T).GetProperty(propertyName);
            
            var property = Expression.Property(param, pInfo);
            
            var newValue = Expression.Constant(value, pInfo.PropertyType);
            
            var body = Expression.Assign(property, newValue);
            
            var final = Expression.Lambda<Action<T>>(body: body, parameters: param);
            
            return final.Compile();
        }

        private class SumContext : IDisposable
        {
            private readonly ODataQueryOptions<T> options;
            private QueryString oldQuery;
            
            public SumContext(ODataQueryOptions<T> options, string filterValue)
            {
                this.options = options;
                oldQuery = options.Request.QueryString;
                options.Request.QueryString = string.IsNullOrEmpty(filterValue)
                    ? QueryString.Empty 
                    : QueryString.Create("$filter", filterValue);
            }

            public ODataQueryOptions<T> GetOptions()
            {
                return new ODataQueryOptions<T>(options.Context, options.Request);
            }

            public void Dispose()
            {
                options.Request.QueryString = oldQuery;
            }
        }
    }
}