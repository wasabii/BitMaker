using Jayrock.Json.Conversion;

namespace BitMaker.Miner
{

    /// <summary>
    /// Return value of 'getwork' RPC sent with data.
    /// </summary>
    public class JsonSubmitWork
    {

        [JsonMemberName("id")]
        public string Id { get; set; }

        [JsonMemberName("result")]
        public bool Result { get; set; }

        [JsonMemberName("error")]
        public string Error { get; set; }

    }

}
