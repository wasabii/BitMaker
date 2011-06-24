#include "stdafx.h"

#include "SseCpuSolver.h"
#include "SseCpuSolver_cpp.h"

using namespace System;
using namespace System::Runtime::InteropServices;

namespace BitMaker
{

    namespace Utils
    {

        namespace Native
        {

            public delegate bool StatusDelegate(unsigned int hashCount);

            public ref class SseCpuHasher sealed abstract
            {

            public:

                static Nullable<unsigned int> Solve(unsigned int* round1State, unsigned char* round1Block1, unsigned int* round2State, unsigned char* round2Block1, StatusDelegate^ status)
                {
                    unsigned int nonce;
                    
                    // create function pointer for 'status', and ensure it doesn't get collected
                    GCHandle statchH = GCHandle::Alloc(status);
                    statusFunc statusPtr = (statusFunc)(void*)Marshal::GetFunctionPointerForDelegate(status);

                    try
                    {
                        if (__Solve(round1State, round1Block1, round2State, round2Block1, &nonce, statusPtr))
                            return Nullable<unsigned int>(nonce);
                        else
                            return Nullable<unsigned int>();
                    }
                    finally
                    {
                        statchH.Free();
                    }
                }

            };

        }

    }

}