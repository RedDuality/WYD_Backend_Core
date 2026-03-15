namespace Core.Model.Util.Exceptions;

public class ObjectDeletedException(string? message = null) : Exception(message ?? "The resource has been deleted.") { }
