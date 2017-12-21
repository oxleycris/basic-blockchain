using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace BlockChain
{
  public class Program
  {
    public static IList<string> blocks = new List<string>();

    public static void Main(string[] args)
    {
      InitBlockChain();
      AddNewBlock("My first block chain string");
      AddNewBlock("My second block chain string");
      AddNewBlock("My third block chain string");
      AddNewBlock("My forth block chain string");

      for (int i = 0; i < blocks.Count; i++)
      {
        Console.WriteLine((i+1) + ": " + blocks[i]);
      }

      Console.ReadLine();
    }

    public static void InitBlockChain()
    {
      // Creates the genesis block - the first block within the chain.
      const string data = "Genesis block";
      const string previousHash = "0000x00000000000000000000000000000000000000000000000000000000000";
      const int index = 0;
      var timeStamp = DateTime.Now.ToString();

      HashBlock(data, timeStamp, previousHash, index);
    }

    public static void AddNewBlock(string data)
    {
      var index = blocks.Count;
      var previousHash = GetLastHash(blocks);

      HashBlock(data, DateTime.Now.ToString(), previousHash, index);
    }

    public static IEnumerable<string> GetAllBlocks()
    {
      return blocks;
    }

    private static void HashBlock(string data, string timestamp, string prevHash, int index)
    {
      var hash = string.Empty;
      var nonce = 0;
      var sha256 = new SHA256Managed();

      while (!IsHashValid(hash))
      {
        var hashString = string.Empty;
        var input = string.Format(data + timestamp + prevHash + index + nonce);
        var sha256Bytes = System.Text.Encoding.Default.GetBytes(input);
        var encodedString = sha256.ComputeHash(sha256Bytes);

        for (int i = 0; i < encodedString.Length; i++)
        {
          hashString += encodedString[i].ToString("X");
        }

        hash = hashString;
        nonce += 1;

        //Console.WriteLine(nonce.ToString() + " | " + hash);
      }

      Console.WriteLine("[VALID] | Nonce: " + nonce + " | Hash: " + hash);
      Console.WriteLine("Press any key to continue...");
      Console.ReadLine();

      blocks.Add(hash);
    }

    private static string GetLastHash(IEnumerable<string> blocks)
    {
      var lastHash = blocks.Last();

      return lastHash;
    }

    private static bool IsHashValid(string hash)
    {
      // Difficulty is the amount of numbers this has to satisfy.
      // This is very low difficulty.
      var hashValid = hash.StartsWith("00");

      return hashValid;
    }
  }
}
