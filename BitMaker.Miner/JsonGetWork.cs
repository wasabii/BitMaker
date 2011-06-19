using Jayrock.Json.Conversion;

namespace BitMaker.Miner
{

    /// <summary>
    /// Return value of 'getwork' RPC.
    /// </summary>
    public class JsonGetWork
    {

        [JsonMemberName("id")]
        public string Id { get; set; }

        [JsonMemberName("result")]
        public JsonGetWorkResult Result { get; set; }

    }

    /// <summary>
    /// Value in 'result' of 'getwork' RPC.
    /// </summary>
    public class JsonGetWorkResult
    {

        [JsonMemberName("midstate")]
        public string Midstate { get; set; }

        [JsonMemberName("data")]
        public string Data { get; set; }

        [JsonMemberName("hash1")]
        public string Hash1 { get; set; }

        [JsonMemberName("target")]
        public string Target { get; set; }

    }

}
