using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.OData.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OData.Client.Extensions
{
    public static class OdataClientExtensions
    {
        private const string TotalSum = "TotalSum";

        public static async Task<QueryOperationResponse<TEntity>> ExecuteAsync<TEntity>(this DataServiceQueryContinuation<TEntity> continuation, DataServiceContext context)
        {
            return (QueryOperationResponse<TEntity>)await context.ExecuteAsync(continuation);
        }
        
        public static async Task<QueryOperationResponse<TEntity>> ExecuteAsync<TEntity>(this IQueryable<TEntity> query)
        {
            var dsQuery = query as DataServiceQuery<TEntity>;
            if (dsQuery == null)
            {
                throw new InvalidOperationException("Argument query should be DataServiceQuery<T>.");
            }
            
            return (QueryOperationResponse<TEntity>) await dsQuery.ExecuteAsync();
        }
        
        public static async Task<TResult> ODataSumAsync<TEntity, TResult>(this IQueryable<TEntity> query, Expression<Func<TEntity, TResult>> expression)
        {
            var dsQuery = query as DataServiceQuery<TEntity>;
            if (dsQuery == null)
            {
                throw new InvalidOperationException("Argument query should be DataServiceQuery<T>.");
            }

            var propertyName = ExtractPropertyName(expression);
            
            var requestUri = dsQuery.RequestUri.OriginalString.MakeRequestString(propertyName);
            
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Accept","application/json; odata.metadata=minimal");
            request.Headers.Add("Connection","Keep-Alive");
            request.Headers.Add("Accept-Charset","UTF-8");
            request.Headers.Add("User-Agent","Microsoft.OData.Client/7.6.0");
            request.Headers.Add("OData-Version","4.0");
            request.Headers.Add("OData-MaxVersion","4.0");
            
            var response = await new HttpClient().SendAsync(request);

            return await response.ExtractSum<TResult>();
        }

        private static string ExtractPropertyName<TEntity, TResult>(Expression<Func<TEntity, TResult>> expression)
        {
            var body = (MemberExpression)expression.Body;
            var pInfo = (PropertyInfo)body.Member;
            return pInfo.Name;
        }

        private static async Task<TResult> ExtractSum<TResult>(this HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Unsuccessful response from server: {content}");
            }
            
            var obj = JsonConvert.DeserializeObject<JArray>(content);

            return obj.Count == 0 
                ? default(TResult)
                : obj[0][TotalSum].Value<TResult>();
        }

        private static string MakeRequestString(this string uri, string propertyName)
        {
            var filterValue = uri.ExtractFilterValue();
            
            var uriWithoutQuery = uri.Split('?').First();

            var request = $"{uriWithoutQuery}?$apply=";
            if (!string.IsNullOrEmpty(filterValue))
            {
                request += $"filter({filterValue})/";
            }

            request += $"aggregate({propertyName} with sum as {TotalSum})";

            return request;
        }

        private static string ExtractFilterValue(this string uri)
        {
            var queryStr = uri.Split('?').ElementAtOrDefault(1);

            var filter = queryStr?.Split('&').FirstOrDefault(x => x.Contains("filter"));

            return filter?.Split('=')[1] ?? string.Empty;
        }
    }
}