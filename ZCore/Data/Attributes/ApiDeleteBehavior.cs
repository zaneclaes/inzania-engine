namespace IZ.Core.Data.Attributes;

public enum ApiDeleteBehavior {
    /// <summary>
    ///     Sets foreign key values to <see langword="null" /> as appropriate when changes are made to tracked entities and creates
    ///     a non-cascading foreign key constraint in the database. This is the default for optional relationships.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" />, <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" />, and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> all have essentially the same behavior, except
    ///         that <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" /> and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> will configure the foreign key constraint as "RESTRICT" or "NO ACTION"
    ///         respectively for databases that support this, while <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" /> uses the default database setting for foreign
    ///         key constraints.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    ClientSetNull,
    /// <summary>
    ///     Sets foreign key values to <see langword="null" /> as appropriate when changes are made to tracked entities and creates
    ///     a non-cascading foreign key constraint in the database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" />, <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" />, and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> all have essentially the same behavior, except
    ///         that <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" /> and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> will configure the foreign key constraint as "RESTRICT" or "NO ACTION"
    ///         respectively for databases that support this, while <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" /> uses the default database setting for foreign
    ///         key constraints.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    Restrict,
    /// <summary>
    ///     Sets foreign key values to <see langword="null" /> as appropriate when changes are made to tracked entities and creates
    ///     a foreign key constraint in the database that propagates <see langword="null" /> values from principals to dependents.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Not all database support propagation of <see langword="null" /> values, and some databases that do have restrictions
    ///         on when it can be used. For example, when using SQL Server, it is difficult to use <see langword="null" /> propagation
    ///         without creating multiple cascade paths.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    SetNull,
    /// <summary>
    ///     Automatically deletes dependent entities when the principal is deleted or the relationship to the principal is severed,
    ///     and creates a foreign key constraint in the database with cascading deletes enabled. This is the default for
    ///     required relationships.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Some databases have restrictions on when cascading deletes can be used. For example, SQL Server has limited
    ///         support for multiple cascade paths. Consider using <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientCascade" /> instead for these cases.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    Cascade,
    /// <summary>
    ///     Automatically deletes dependent entities when the principal is deleted or the relationship to the principal is severed,
    ///     but creates a non-cascading foreign key constraint in the database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Consider using this option when database restrictions prevent the use of <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade" />.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    ClientCascade,
    /// <summary>
    ///     Sets foreign key values to <see langword="null" /> as appropriate when changes are made to tracked entities and creates
    ///     a non-cascading foreign key constraint in the database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" />, <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" />, and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> all have essentially the same behavior, except
    ///         that <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict" /> and <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction" /> will configure the foreign key constraint as "RESTRICT" or "NO ACTION"
    ///         respectively for databases that support this, while <see cref="F:Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull" /> uses the default database setting for foreign
    ///         key constraints.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    NoAction,
    /// <summary>
    ///     Tracked dependents are not deleted and their foreign key values are not set to <see langword="null" /> when deleting
    ///     principal entities. A non-cascading foreign key constraint is created in the database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It is unusual to use this option and will often result in exceptions when saving changes to the database unless
    ///         additional work is done.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-cascading">EF Core cascade deletes and deleting orphans</see> for more information
    ///         and examples.
    ///     </para>
    /// </remarks>
    ClientNoAction,
}
