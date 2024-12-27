#region

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Api;

public interface IPreFetched<TEntity, TProperty> : ITuneQueryable<TEntity> where TEntity : class { }

public static class PreFetches {

  public static IPreFetched<TEntity, TProperty> Fetch<TEntity, TProperty>(
    this ITuneQueryable<TEntity> source,
    Expression<Func<TEntity, TProperty>> navigationPropertyPath
  ) where TEntity : ApiObject => source.Repository.QueryInclude(source, navigationPropertyPath);

  public static IPreFetched<TEntity, TProperty> ThenFetch<TEntity, TPreviousProperty, TProperty>(
    this IPreFetched<TEntity, List<TPreviousProperty>> source,
    Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath
  ) where TEntity : ApiObject => source.Repository.QueryThenIncludeMany(source, navigationPropertyPath);

  public static IPreFetched<TEntity, TProperty> ThenFetch<TEntity, TPreviousProperty, TProperty>(
    this IPreFetched<TEntity, TPreviousProperty> source,
    Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath
  ) where TEntity : ApiObject =>
    source.Repository.QueryThenInclude(source, navigationPropertyPath);
}
