using System.Collections.Generic;

namespace ONIModPack.Core.Services
{
    public interface IRepository<T> where T : class
    {
        IEnumerable<T> GetAll();
        
        T Get(string id);
        
        void Save(T obj);
        
        void Delete(string id);
        
        bool Exists(string id);
    }
    
    public interface IRepository<T, TId> where T : class
    {
        IEnumerable<T> GetAll();
        
        T Get(TId id);
        
        void Save(T obj);
        
        void Delete(TId id);
        
        bool Exists(TId id);
    }
}
