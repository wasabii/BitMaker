#include "Stdafx.h"
#include "sha256_sse_x64.h"

#pragma managed(push, off)

static const uint32_t sha256_consts[] =
{
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
    0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
    0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
    0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
    0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
    0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
    0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
    0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
    0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
};

#define SHR(word, shift) (_mm_srli_epi32(word, shift))

//static inline __m128i SHR(__m128i word, int shift)
//{
//    return _mm_srli_epi32(word, shift);
//}

#define ROTR(word, shift) (_mm_or_si128(_mm_srli_epi32(word, shift), _mm_slli_epi32(word, 32 - shift)))

//static inline __m128i ROTR(__m128i word, int shift)
//{
//    return _mm_or_si128(_mm_srli_epi32(word, shift), _mm_slli_epi32(word, 32 - shift));
//}

#define Ch(b, c, d) (_mm_xor_si128(_mm_and_si128(b, c), _mm_andnot_si128(b, d)))

//static inline __m128i Ch(__m128i b, __m128i c, __m128i d)
//{
//
//    return _mm_xor_si128(_mm_and_si128(b, c), _mm_andnot_si128(b, d));
//}

#define Maj(b, c, d) (_mm_xor_si128(_mm_xor_si128(_mm_and_si128(b, c), _mm_and_si128(b, d)), _mm_and_si128(c, d)))

//static inline __m128i Maj(__m128i b, __m128i c, __m128i d)
//{
//    return _mm_xor_si128(_mm_xor_si128(_mm_and_si128(b, c), _mm_and_si128(b, d)), _mm_and_si128(c, d));
//}

#define Sigma0(x) (_mm_xor_si128(_mm_xor_si128(ROTR(x, 2), ROTR(x, 13)), ROTR(x, 22)))

//static inline __m128i Sigma0(__m128i x)
//{
//    return _mm_xor_si128(_mm_xor_si128(ROTR(x, 2), ROTR(x, 13)), ROTR(x, 22));
//}

#define Sigma1(x) (_mm_xor_si128(_mm_xor_si128(ROTR(x, 6), ROTR(x, 11)), ROTR(x, 25)))

//static inline __m128i Sigma1(__m128i x)
//{
//    return _mm_xor_si128(_mm_xor_si128(ROTR(x, 6), ROTR(x, 11)), ROTR(x, 25));
//}

#define sigma0(x) (_mm_xor_si128(_mm_xor_si128(ROTR(x, 7), ROTR(x, 18)), SHR(x, 3)))

//static inline __m128i sigma0(__m128i x)
//{
//    return _mm_xor_si128(_mm_xor_si128(ROTR(x, 7), ROTR(x, 18)), SHR(x, 3));
//}

#define sigma1(x) (_mm_xor_si128(_mm_xor_si128(ROTR(x, 17), ROTR(x, 19)), SHR(x, 10)))

//static inline __m128i sigma1(__m128i x)
//{
//    return _mm_xor_si128(_mm_xor_si128(ROTR(x, 17), ROTR(x, 19)), SHR(x, 10));
//}

#define add2(a, b) (_mm_add_epi32(a, b))

//static inline __m128i add2(__m128i a, __m128i b)
//{
//    return _mm_add_epi32(a, b);
//}

#define add4(a, b, c, d) (add2(add2(a, b), add2(c, d)))

//static inline __m128i add4(__m128i a, __m128i b, __m128i c, __m128i d)
//{
//    return _mm_add_epi32(_mm_add_epi32(_mm_add_epi32(a, b), c), d);
//}

#define add5(a, b, c, d, e) add2(add4(a, b, c, d), e)

//static inline __m128i add5(__m128i a, __m128i b, __m128i c, __m128i d, __m128i e)
//{
//    return _mm_add_epi32(_mm_add_epi32(_mm_add_epi32(_mm_add_epi32(a, b), c), d), e);
//}


static inline void __sha256_transform(__m128i *state, __m128i *block, __m128i *output)
{
    __m128i W[64], t1, t2;
    
    W[0]  = block[ 0];
    W[1]  = block[ 1];
    W[2]  = block[ 2];
    W[3]  = block[ 3];
    W[4]  = block[ 4];
    W[5]  = block[ 5];
    W[6]  = block[ 6];
    W[7]  = block[ 7];
    W[8]  = block[ 8];
    W[9]  = block[ 9];
    W[10] = block[10];
    W[11] = block[11];
    W[12] = block[12];
    W[13] = block[13];
    W[14] = block[14];
    W[15] = block[15];
    
    W[16] = add4(sigma1(W[16 - 2]), W[16 - 7], sigma0(W[16 - 15]), W[16 - 16]);
    W[17] = add4(sigma1(W[17 - 2]), W[17 - 7], sigma0(W[17 - 15]), W[17 - 16]);
    W[18] = add4(sigma1(W[18 - 2]), W[18 - 7], sigma0(W[18 - 15]), W[18 - 16]);
    W[19] = add4(sigma1(W[19 - 2]), W[19 - 7], sigma0(W[19 - 15]), W[19 - 16]);
    W[20] = add4(sigma1(W[20 - 2]), W[20 - 7], sigma0(W[20 - 15]), W[20 - 16]);
    W[21] = add4(sigma1(W[21 - 2]), W[21 - 7], sigma0(W[21 - 15]), W[21 - 16]);
    W[22] = add4(sigma1(W[22 - 2]), W[22 - 7], sigma0(W[22 - 15]), W[22 - 16]);
    W[23] = add4(sigma1(W[23 - 2]), W[23 - 7], sigma0(W[23 - 15]), W[23 - 16]);
    W[24] = add4(sigma1(W[24 - 2]), W[24 - 7], sigma0(W[24 - 15]), W[24 - 16]);
    W[25] = add4(sigma1(W[25 - 2]), W[25 - 7], sigma0(W[25 - 15]), W[25 - 16]);
    W[26] = add4(sigma1(W[26 - 2]), W[26 - 7], sigma0(W[26 - 15]), W[26 - 16]);
    W[27] = add4(sigma1(W[27 - 2]), W[27 - 7], sigma0(W[27 - 15]), W[27 - 16]);
    W[28] = add4(sigma1(W[28 - 2]), W[28 - 7], sigma0(W[28 - 15]), W[28 - 16]);
    W[29] = add4(sigma1(W[29 - 2]), W[29 - 7], sigma0(W[29 - 15]), W[29 - 16]);
    W[30] = add4(sigma1(W[30 - 2]), W[30 - 7], sigma0(W[30 - 15]), W[30 - 16]);
    W[31] = add4(sigma1(W[31 - 2]), W[31 - 7], sigma0(W[31 - 15]), W[31 - 16]);
    W[32] = add4(sigma1(W[32 - 2]), W[32 - 7], sigma0(W[32 - 15]), W[32 - 16]);
    W[33] = add4(sigma1(W[33 - 2]), W[33 - 7], sigma0(W[33 - 15]), W[33 - 16]);
    W[34] = add4(sigma1(W[34 - 2]), W[34 - 7], sigma0(W[34 - 15]), W[34 - 16]);
    W[35] = add4(sigma1(W[35 - 2]), W[35 - 7], sigma0(W[35 - 15]), W[35 - 16]);
    W[36] = add4(sigma1(W[36 - 2]), W[36 - 7], sigma0(W[36 - 15]), W[36 - 16]);
    W[37] = add4(sigma1(W[37 - 2]), W[37 - 7], sigma0(W[37 - 15]), W[37 - 16]);
    W[38] = add4(sigma1(W[38 - 2]), W[38 - 7], sigma0(W[38 - 15]), W[38 - 16]);
    W[39] = add4(sigma1(W[39 - 2]), W[39 - 7], sigma0(W[39 - 15]), W[39 - 16]);
    W[40] = add4(sigma1(W[40 - 2]), W[40 - 7], sigma0(W[40 - 15]), W[40 - 16]);
    W[41] = add4(sigma1(W[41 - 2]), W[41 - 7], sigma0(W[41 - 15]), W[41 - 16]);
    W[42] = add4(sigma1(W[42 - 2]), W[42 - 7], sigma0(W[42 - 15]), W[42 - 16]);
    W[43] = add4(sigma1(W[43 - 2]), W[43 - 7], sigma0(W[43 - 15]), W[43 - 16]);
    W[44] = add4(sigma1(W[44 - 2]), W[44 - 7], sigma0(W[44 - 15]), W[44 - 16]);
    W[45] = add4(sigma1(W[45 - 2]), W[45 - 7], sigma0(W[45 - 15]), W[45 - 16]);
    W[46] = add4(sigma1(W[46 - 2]), W[46 - 7], sigma0(W[46 - 15]), W[46 - 16]);
    W[47] = add4(sigma1(W[47 - 2]), W[47 - 7], sigma0(W[47 - 15]), W[47 - 16]);
    W[48] = add4(sigma1(W[48 - 2]), W[48 - 7], sigma0(W[48 - 15]), W[48 - 16]);
    W[49] = add4(sigma1(W[49 - 2]), W[49 - 7], sigma0(W[49 - 15]), W[49 - 16]);
    W[50] = add4(sigma1(W[50 - 2]), W[50 - 7], sigma0(W[50 - 15]), W[50 - 16]);
    W[51] = add4(sigma1(W[51 - 2]), W[51 - 7], sigma0(W[51 - 15]), W[51 - 16]);
    W[52] = add4(sigma1(W[52 - 2]), W[52 - 7], sigma0(W[52 - 15]), W[52 - 16]);
    W[53] = add4(sigma1(W[53 - 2]), W[53 - 7], sigma0(W[53 - 15]), W[53 - 16]);
    W[54] = add4(sigma1(W[54 - 2]), W[54 - 7], sigma0(W[54 - 15]), W[54 - 16]);
    W[55] = add4(sigma1(W[55 - 2]), W[55 - 7], sigma0(W[55 - 15]), W[55 - 16]);
    W[56] = add4(sigma1(W[56 - 2]), W[56 - 7], sigma0(W[56 - 15]), W[56 - 16]);
    W[57] = add4(sigma1(W[57 - 2]), W[57 - 7], sigma0(W[57 - 15]), W[57 - 16]);
    W[58] = add4(sigma1(W[58 - 2]), W[58 - 7], sigma0(W[58 - 15]), W[58 - 16]);
    W[59] = add4(sigma1(W[59 - 2]), W[59 - 7], sigma0(W[59 - 15]), W[59 - 16]);
    W[60] = add4(sigma1(W[60 - 2]), W[60 - 7], sigma0(W[60 - 15]), W[60 - 16]);
    W[61] = add4(sigma1(W[61 - 2]), W[61 - 7], sigma0(W[61 - 15]), W[61 - 16]);
    W[62] = add4(sigma1(W[62 - 2]), W[62 - 7], sigma0(W[62 - 15]), W[62 - 16]);
    W[63] = add4(sigma1(W[63 - 2]), W[63 - 7], sigma0(W[63 - 15]), W[63 - 16]);

    // read existing state
    __m128i a = state[0];
    __m128i b = state[1];
    __m128i c = state[2];
    __m128i d = state[3];
    __m128i e = state[4];
    __m128i f = state[5];
    __m128i g = state[6];
    __m128i h = state[7];
    
    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0x428a2f98), W[0]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0x71374491), W[1]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0xb5c0fbcf), W[2]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0xe9b5dba5), W[3]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x3956c25b), W[4]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0x59f111f1), W[5]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x923f82a4), W[6]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0xab1c5ed5), W[7]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0xd807aa98), W[8]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0x12835b01), W[9]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0x243185be), W[10]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0x550c7dc3), W[11]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x72be5d74), W[12]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0x80deb1fe), W[13]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x9bdc06a7), W[14]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0xc19bf174), W[15]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0xe49b69c1), W[16]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0xefbe4786), W[17]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0x0fc19dc6), W[18]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0x240ca1cc), W[19]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x2de92c6f), W[20]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0x4a7484aa), W[21]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x5cb0a9dc), W[22]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0x76f988da), W[23]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0x983e5152), W[24]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0xa831c66d), W[25]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0xb00327c8), W[26]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0xbf597fc7), W[27]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0xc6e00bf3), W[28]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0xd5a79147), W[29]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x06ca6351), W[30]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0x14292967), W[31]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0x27b70a85), W[32]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0x2e1b2138), W[33]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0x4d2c6dfc), W[34]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0x53380d13), W[35]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x650a7354), W[36]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0x766a0abb), W[37]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x81c2c92e), W[38]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0x92722c85), W[39]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0xa2bfe8a1), W[40]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0xa81a664b), W[41]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0xc24b8b70), W[42]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0xc76c51a3), W[43]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0xd192e819), W[44]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0xd6990624), W[45]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0xf40e3585), W[46]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0x106aa070), W[47]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0x19a4c116), W[48]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0x1e376c08), W[49]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0x2748774c), W[50]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0x34b0bcb5), W[51]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x391c0cb3), W[52]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0x4ed8aa4a), W[53]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0x5b9cca4f), W[54]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0x682e6ff3), W[55]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);

    t1 = add5(h, Sigma1(e), Ch(e, f, g), _mm_set1_epi32(0x748f82ee), W[56]);
    t2 = add2(Sigma0(a), Maj(a, b, c)); d = add2(d, t1); h = add2(t1, t2);
    t1 = add5(g, Sigma1(d), Ch(d, e, f), _mm_set1_epi32(0x78a5636f), W[57]);
    t2 = add2(Sigma0(h), Maj(h, a, b)); c = add2(c, t1); g = add2(t1, t2);
    t1 = add5(f, Sigma1(c), Ch(c, d, e), _mm_set1_epi32(0x84c87814), W[58]);
    t2 = add2(Sigma0(g), Maj(g, h, a)); b = add2(b, t1); f = add2(t1, t2);
    t1 = add5(e, Sigma1(b), Ch(b, c, d), _mm_set1_epi32(0x8cc70208), W[59]);
    t2 = add2(Sigma0(f), Maj(f, g, h)); a = add2(a, t1); e = add2(t1, t2);
    t1 = add5(d, Sigma1(a), Ch(a, b, c), _mm_set1_epi32(0x90befffa), W[60]);
    t2 = add2(Sigma0(e), Maj(e, f, g)); h = add2(h, t1); d = add2(t1, t2);
    t1 = add5(c, Sigma1(h), Ch(h, a, b), _mm_set1_epi32(0xa4506ceb), W[61]);
    t2 = add2(Sigma0(d), Maj(d, e, f)); g = add2(g, t1); c = add2(t1, t2);
    t1 = add5(b, Sigma1(g), Ch(g, h, a), _mm_set1_epi32(0xbef9a3f7), W[62]);
    t2 = add2(Sigma0(c), Maj(c, d, e)); f = add2(f, t1); b = add2(t1, t2);
    t1 = add5(a, Sigma1(f), Ch(f, g, h), _mm_set1_epi32(0xc67178f2), W[63]);
    t2 = add2(Sigma0(b), Maj(b, c, d)); e = add2(e, t1); a = add2(t1, t2);
    
    output[0] = a;
    output[1] = b;
    output[2] = c;
    output[3] = d;
    output[4] = e;
    output[5] = f;
    output[6] = g;
    output[7] = h;
}

#pragma managed(push, on)