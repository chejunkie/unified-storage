namespace UnifiedStorage.Common.Design;

public interface IStorageEndpointFactory<T>
{
    T CreateEndpoint(string configuration);
}