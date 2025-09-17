using MongoDB.Driver;
using MongoDB.Bson;
using Core.Model.Base;
using Microsoft.AspNetCore.Razor.Language;

namespace Core.Components.Database;

public class MongoDbService(MongoDbContext dbContext)
{

    #region util

    public IMongoCollection<TDocument> GetCollection<TDocument>(CollectionName cn)
        where TDocument : BaseEntity
    {

        return dbContext.GetCollection<TDocument>(cn.ToString());
    }

    public IAggregateFluent<TDocument> GetAggregate<TDocument>(CollectionName cn)
    where TDocument : BaseEntity
    {
        return dbContext.GetCollection<TDocument>(cn.ToString()).Aggregate();
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<IClientSessionHandle, Task<T>> transactionalLogic)
    {
        using var session = await dbContext.GetNewSession();

        bool transactionsSupported = dbContext.AreTransactionsSupported();

        if (transactionsSupported)
        {
            try
            {
                session.StartTransaction();
                T result = await transactionalLogic(session);
                await session.CommitTransactionAsync();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during transaction: {ex.Message}");
                await session.AbortTransactionAsync();
                throw;
            }
        }
        else
        {
            Console.WriteLine("Transactions are not supported by the current database technology. Executing logic without a transaction.");
            return await transactionalLogic(session);
        }
    }

    public async Task ConfirmExists<TDocument>(CollectionName cn, string stringId)
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        var exists = false;
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            exists = await collection
                .Find(Builders<TDocument>.Filter.Eq(x => x.Id, new ObjectId(stringId)))
                .Project(x => x.Id)   // only retrieve _id from index
                .Limit(1)
                .AnyAsync();
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing CheckExistance on collection '{collectionName}'.",
                ex
            );
        }

        if (!exists)
        {
            throw new Exception(
                $"Document with id '{stringId}' not found in collection '{collectionName}'"
            );
        }

    }

    #endregion


    #region create

    public async Task<TDocument> CreateOneAsync<TDocument>(CollectionName cn, TDocument newDocument, IClientSessionHandle? session)
        where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            if (session != null)
            {
                await collection.InsertOneAsync(session, newDocument);
            }
            else
            {
                await collection.InsertOneAsync(newDocument);
            }


            return newDocument;
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed for document in collection '{collectionName}'.",
                ex
            );
        }
    }

    public async Task<List<TDocument>> CreateManyAsync<TDocument>(CollectionName cn, List<TDocument> newDocuments)
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            await collection.InsertManyAsync(newDocuments);
            return newDocuments;
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed for document in collection '{collectionName}'.",
                ex
            );
        }
    }

    #endregion


    #region update

    public async Task<TDocument> FindOneByIdAndUpdateAsync<TDocument>(
        CollectionName collectionName,
        ObjectId objectId,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle? session = null,
        FindOneAndUpdateOptions<TDocument>? options = null,
        bool saveUpdates = true
    )
    where TDocument : BaseEntity
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, objectId);
        return await FindOneAndUpdateAsync(collectionName, filter, updateDefinition, session, options, saveUpdates)
            ?? throw new KeyNotFoundException(
                $"Document with id '{objectId}' not found in collection '{collectionName}' for patch update."
            );

    }

    // inherently transactional on a sigle document
    // returns the document
    public async Task<TDocument> FindOneAndUpdateAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle? session = null,
        FindOneAndUpdateOptions<TDocument>? options = null,
        bool saveUpdates = true
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            var combinedUpdateDefinition = updateDefinition;

            if (typeof(BaseDateEntity).IsAssignableFrom(typeof(TDocument)) && saveUpdates)
            {
                combinedUpdateDefinition = Builders<TDocument>.Update.Combine(
                    updateDefinition,
                    Builders<TDocument>.Update.Set("updatedAt", DateTimeOffset.UtcNow)
                );
            }

            if (session != null)
            {
                return await collection.FindOneAndUpdateAsync(
                    session,
                    filter,
                    updateDefinition,
                    options ?? new FindOneAndUpdateOptions<TDocument> { ReturnDocument = ReturnDocument.After }
                );
            }

            return await collection.FindOneAndUpdateAsync(
                filter,
                updateDefinition,
                options ?? new FindOneAndUpdateOptions<TDocument> { ReturnDocument = ReturnDocument.After }
            );

        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing FindOneAndUpdate on collection '{collectionName}'.",
                ex
            );
        }
    }

    // inherently transactional on a sigle document

    public async Task<UpdateResult> UpdateOneByIdAsync<TDocument>(
        CollectionName cn,
        ObjectId objectId,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle? session = null,
        bool saveUpdates = true
    )
    where TDocument : BaseEntity
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, objectId);
        return await UpdateOneAsync(cn, filter, updateDefinition, session, saveUpdates);
    }

    public async Task<UpdateResult> UpdateOneAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle? session = null,
        bool saveUpdates = true
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            var combinedUpdateDefinition = updateDefinition;

            if (typeof(BaseDateEntity).IsAssignableFrom(typeof(TDocument)) && saveUpdates)
            {
                combinedUpdateDefinition = Builders<TDocument>.Update.Combine(
                    updateDefinition,
                    Builders<TDocument>.Update.Set("updatedAt", DateTimeOffset.UtcNow)
                );
            }

            if (session != null)
            {
                return await collection.UpdateOneAsync(
                    session,
                    filter,
                    combinedUpdateDefinition
                );
            }

            return await collection.UpdateOneAsync(
                filter,
                combinedUpdateDefinition
            );
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing UpdateOne on collection '{collectionName}'.",
                ex
            );
        }
    }

    public async Task<UpdateResult> UpdateManyAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filterDefinition,
        UpdateDefinition<TDocument> updateDefinition,
        UpdateOptions<TDocument>? options = null,
        bool saveUpdates = true
    ) where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            var combinedUpdateDefinition = updateDefinition;

            if (typeof(BaseDateEntity).IsAssignableFrom(typeof(TDocument)) && saveUpdates)
            {
                combinedUpdateDefinition = Builders<TDocument>.Update.Combine(
                    updateDefinition,
                    Builders<TDocument>.Update.Set("updatedAt", DateTimeOffset.UtcNow)
                );
            }

            return await collection.UpdateManyAsync(filterDefinition, combinedUpdateDefinition, options: options);

        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing bulk update in collection '{collectionName}'.",
                ex
            );
        }
    }

    #endregion

    #region retrieve

    public async Task<TDocument> RetrieveByIdAsync<TDocument>(
        CollectionName collectionName,
        string stringId
    )
    where TDocument : BaseEntity
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, new ObjectId(stringId));
        return await FindOneAsync(collectionName, filter);
    }

    public async Task<TDocument> FindOneAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            return await collection.Find(filter).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException(
                    $"Document not found from collection '{collectionName}'."
                );
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while retrieving for documents in collection '{collectionName}'.",
                ex
            );
        }
    }

    public async Task<List<TDocument>> RetrieveByIdsAsync<TDocument>(
        CollectionName cn,
        List<string> stringIds
    )
    where TDocument : BaseEntity
    {
        var objectIds = stringIds.Select(id => new ObjectId(id)).ToList();
        return await RetrieveByIdsAsync<TDocument>(cn, objectIds);
    }

    public async Task<List<TDocument>> RetrieveByIdsAsync<TDocument>(
        CollectionName collectionName,
        List<ObjectId> objectIds
    )
    where TDocument : BaseEntity
    {
        var filter = Builders<TDocument>.Filter.In(doc => doc.Id, objectIds);
        return await FindAsync(collectionName, filter);
    }

    public async Task<List<TDocument>> FindAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter
    )
     where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            return await collection.Find(filter).ToListAsync();
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while retrieving for documents in collection '{collectionName}'.",
                ex
            );
        }
    }

    public async Task<List<TDocument>> FindForPaginationAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter,
        SortDefinition<TDocument> sortDefinition,
        int? pageNumber,
        int? pageSize
    )
     where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            var findFluent = collection.Find(filter)
                                       .Sort(sortDefinition);

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                findFluent = findFluent.Skip((pageNumber.Value - 1) * pageSize.Value)
                                       .Limit(pageSize.Value);
            }

            return await findFluent.ToListAsync();
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while retrieving for documents in collection '{collectionName}'.",
                ex
            );
        }
    }

    #endregion

}