using MongoDB.Driver;
using MongoDB.Bson;
using Core.Model.Base;

namespace Core.Services.Util;

public class MongoDbService(MongoDbContext dbContext)
{
    public async Task ExecuteInTransactionAsync(Func<Task> transactionalLogic)
    {
        using var session = await dbContext.GetNewSession();
        session.StartTransaction();
        try
        {
            await transactionalLogic();
            await session.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during transaction: {ex.Message}");
            await session.AbortTransactionAsync();
            throw;
        }
    }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
        where TDocument : BaseEntity
    {
        return dbContext.GetCollection<TDocument>(collectionName);
    }

    public IAggregateFluent<TDocument> GetAggregate<TDocument>(string collectionName)
    where TDocument : BaseEntity
    {
        return dbContext.GetCollection<TDocument>(collectionName).Aggregate();
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

    public async Task<TDocument> CreateOneAsync<TDocument>(string collectionName, TDocument newDocument, IClientSessionHandle? session)
        where TDocument : BaseEntity
    {
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

    public async Task<List<TDocument>> CreateManyAsync<TDocument>(string collectionName, List<TDocument> newDocuments)
    where TDocument : BaseEntity
    {
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


    public async Task<TDocument> FindOneAndUpdateAsync<TDocument>(
        string collectionName,
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> updateDefinition,
        FindOneAndUpdateOptions<TDocument>? options = null
    )
    where TDocument : BaseEntity
    {
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

    public async Task<TDocument> PatchUpdateAsync<TDocument>(
        string collectionName,
        string stringId,
        UpdateDefinition<TDocument> updateDefinition,
        IClientSessionHandle session
    )
    where TDocument : BaseEntity
    {
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, new ObjectId(stringId));

            return await collection.FindOneAndUpdateAsync(
                    session,
                    filter,
                    updateDefinition,
                    new FindOneAndUpdateOptions<TDocument> { ReturnDocument = ReturnDocument.After }
                )
                ?? throw new KeyNotFoundException(
                    $"Document with id '{stringId}' not found in collection '{collectionName}' for patch update."
                );
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while performing patch update for document with id '{stringId}' in collection '{collectionName}'.",
                ex
            );
        }
    }


    public async Task<UpdateResult> UpdateManyAsync<TDocument>(
        string collectionName,
        FilterDefinition<TDocument> filterDefinition,
        UpdateDefinition<TDocument> updateDefinition
    ) where TDocument : BaseEntity
    {
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
        string collectionName,
        string stringId
    )
    where TDocument : BaseEntity
    {
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, new ObjectId(stringId));

            return await collection.Find(filter).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException(
                    $"Document not found from collection '{collectionName}'."
                );
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while retrieving document by field from collection '{collectionName}'.",
                ex
            );
        }
    }



    public async Task<List<TDocument>> FindAsync<TDocument>(
        string collectionName,
        FilterDefinition<TDocument> filter
    )
     where TDocument : BaseEntity
    {
        try
        {
            var collection = dbContext.GetCollection<TDocument>(collectionName);

            var matchingDocuments = await collection.Find(filter).ToListAsync();

            return matchingDocuments;
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