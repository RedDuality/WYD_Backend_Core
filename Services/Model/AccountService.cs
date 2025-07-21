using Model;
using Database;
using Microsoft.EntityFrameworkCore;

namespace Service;
public class AccountService
{

    WydDbContext db;

    public AccountService(WydDbContext wydDbContext)
    {
        db = wydDbContext;
    }

    public Account? RetrieveOrNull(int id)
    {
        return db.Accounts.Find(id);
    }
    public Account? Retrieve(string uid)
    {
        try
        {
            return db.Accounts.SingleOrDefault(a => a.Uid == uid);
        }
        catch (InvalidOperationException e)
        {
            throw new Exception("More than one account with the given Uid is present" + e.Message);
        }
    }

    public Account Create(Account account)
    {
        try
        {
            if (string.IsNullOrEmpty(account.Mail) || string.IsNullOrEmpty(account.Uid))
            {
                throw new ArgumentException("Account Mail and Uid cannot be empty.");
            }

            db.Accounts.Add(account);
            db.SaveChanges();
            return account;
        }
        catch (DbUpdateException dbEx)
        {
            // Handle database-related issues, e.g., constraint violations
            throw new InvalidOperationException("Error saving account to the database.", dbEx);
        }
        catch (ArgumentException argEx)
        {
            // Handle invalid input (e.g., empty Mail or Uid)
            throw new ArgumentException("Invalid account data provided.", argEx);
        }
        catch (Exception ex)
        {
            // Handle any unexpected exceptions
            throw new Exception("An unexpected error occurred while creating the account.", ex);
        }
    }

}