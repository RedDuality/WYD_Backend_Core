using MongoDB.Driver;
using MongoDB.Bson;
using Core.Model.Base;

namespace Core.Services.Database;

public class MongoDbService(MongoDbContext dbContext)
{
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

    public async Task<TDocument> CreateOneAsync<TDocument>(CollectionName cn, TDocument newDocument, IClientSessionHandle? session)
        where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            await collection.InsertOneAsync(session, newDocument);
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


    //filter more complex, no session
    public async Task<TDocument> FindOneAndUpdateAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> updateDefinition,
        FindOneAndUpdateOptions<TDocument>? options = null
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            return await collection.FindOneAndUpdateAsync(filter, updateDefinition, options);
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing FindOneAndUpdate on collection '{collectionName}'.",
                ex
            );
        }
    }

    public async Task<TDocument> PatchUpdateByIdAsync<TDocument>(
        CollectionName collectionName,
        ObjectId objectId,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle session
    )
    where TDocument : BaseEntity
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, objectId);
        return await PatchUpdateAsync(collectionName, filter, updateDefinition, session)
            ?? throw new KeyNotFoundException(
                $"Document with id '{objectId}' not found in collection '{collectionName}' for patch update."
            );

    }

    public async Task<TDocument> PatchUpdateAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filterDefinition,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle session
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            return await collection.FindOneAndUpdateAsync(
                    session,
                    filterDefinition,
                    updateDefinition,
                    new FindOneAndUpdateOptions<TDocument> { ReturnDocument = ReturnDocument.After }
                );
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing patch update for document in collection '{collectionName}'.",
                ex
            );
        }
    }


    public async Task<UpdateResult> UpdateManyAsync<TDocument>(
        CollectionName cn,
        FilterDefinition<TDocument> filterDefinition,
        UpdateDefinition<TDocument> updateDefinition
    ) where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);
            return await collection.UpdateManyAsync(filterDefinition, updateDefinition);
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing bulk update in collection '{collectionName}'.",
                ex
            );
        }
    }

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

}