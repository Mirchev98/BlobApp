using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobApp.Services.Interfaces
{
    public interface IEncryptionService
    {
        byte[] EncryptData(byte[] data);

        byte[] DecryptData(byte[] encryptedData);
    }
}
