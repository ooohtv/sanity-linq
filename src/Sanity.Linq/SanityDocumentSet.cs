﻿// Copywrite 2018 Oslofjord Operations AS

// This file is part of Sanity LINQ (https://github.com/oslofjord/sanity-linq).

//  Sanity LINQ is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.

//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//  GNU General Public License for more details.

//  You should have received a copy of the GNU General Public License
//  along with this program.If not, see<https://www.gnu.org/licenses/>.

using Sanity.Linq.DTOs;
using Sanity.Linq.Extensions;
using Sanity.Linq.Internal;
using Sanity.Linq.Mutations;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sanity.Linq
{

    public abstract class SanityDocumentSet
    {
        public SanityDataContext Context { get; protected set; }
    }

    public class SanityDocumentSet<TDoc> : SanityDocumentSet, IOrderedQueryable<TDoc>
    {
        public IQueryProvider Provider { get; private set; }
        public Expression Expression { get; private set; }

        /// <summary> 
        /// This constructor is called by the client to create the data source. 
        /// </summary> 
        public SanityDocumentSet(SanityOptions options)
        {
            Context = new SanityDataContext(options, false);
            Provider = new SanityQueryProvider(typeof(TDoc), Context);
            Expression = Expression.Constant(this);
        }

        public SanityDocumentSet(SanityDataContext context)
        {
            Context = context;
            Provider = new SanityQueryProvider(typeof(TDoc), context);
            Expression = Expression.Constant(this);
        }

        /// <summary> 
        /// This constructor is called by Provider.CreateQuery(). 
        /// </summary> 
        /// <param name="expression"></param>
        public SanityDocumentSet(SanityQueryProvider provider, Expression expression)
        {
            Context = provider.Context;
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            if (!typeof(IQueryable<TDoc>).IsAssignableFrom(expression.Type))
            {
                throw new ArgumentOutOfRangeException("expression");
            }

            Provider = provider;
            Expression = expression;
        }

        public Type ElementType
        {
            get { return typeof(TDoc); }
        }

        public async Task<IEnumerable<TDoc>> ExecuteAsync()
        {
            var results = (await ((SanityQueryProvider)Provider).ExecuteAsync<IEnumerable<TDoc>>(Expression).ConfigureAwait(false)) ?? new TDoc[] { };
            return FilterResults(results);
        }
        
        public async Task<int> ExecuteCountAsync()
        {
            var countMethod = TypeSystem.GetMethod(nameof(Queryable.Count)).MakeGenericMethod(typeof(TDoc));
            var exp = Expression.Call(null,countMethod, Expression);
            return (await ((SanityQueryProvider)Provider).ExecuteAsync<int>(exp).ConfigureAwait(false));
            
        }

        public async Task<long> ExecuteLongCountAsync()
        {
            var countMethod = TypeSystem.GetMethod(nameof(Queryable.LongCount)).MakeGenericMethod(typeof(TDoc));
            var exp = Expression.Call(null, countMethod, Expression);
            return (await ((SanityQueryProvider)Provider).ExecuteAsync<long>(exp).ConfigureAwait(false));

        }

        public SanityDocumentSet<TDoc> Include<TProperty>(Expression<Func<TDoc, TProperty>> property)
        {
            var includeMethod = typeof(SanityDocumentSetExtensions).GetMethod("Include").MakeGenericMethod(typeof(TDoc), typeof(TProperty));
            var exp = Expression.Call(null, includeMethod, Expression, property);
            Expression = exp;
            return this;
           
        }

        public IEnumerator<TDoc> GetEnumerator()
        {
            var results = ((Provider.Execute<IEnumerable<TDoc>>(Expression)) ?? new TDoc[] { });
            return FilterResults(results).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((Provider.Execute<IEnumerable>(Expression)) ?? new object[] { }).GetEnumerator();
        }

        protected virtual IEnumerable<TDoc> FilterResults(IEnumerable<TDoc> results)
        {
            //TODO: Consider merging additions / updates with data source results
            // A full implementation would also require reevaluating ordering and slicing on client side...
            foreach (var item in results)
            {
                yield return item;
            }
        }

        public TDoc Get(string id)
        {
            return GetAsync(id).Result;
        }

        public async Task<TDoc> GetAsync(string id)
        {
            return await this.Where(d => d.SanityId() == id).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public SanityMutationBuilder<TDoc> Mutations
        {
            get
            {
                return Context.Mutations.For<TDoc>();
            }
        }

    }
}
