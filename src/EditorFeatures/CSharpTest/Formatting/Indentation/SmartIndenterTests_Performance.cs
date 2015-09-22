﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Formatting.Indentation
{
    public partial class SmartIndenterTests
    {
        // TODO: Author this as a performance test.
        [Fact]
        [Trait(Traits.Feature, Traits.Features.SmartIndent)]
        public void RegionPerformance()
        {
            var code =
            #region very long sample code
 @"using System;
using System.Collections.Generic;

class Class1
{
#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }
    #endregion
    };
#endif

#if (false && Var1)
    Dictionary<int, List<string>> x = new Dictionary<int, List<string>>() {
    #region Region 1
        {
            1,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 2
        {
            2,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 3
        {
            3,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 4
        {
            4,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 5
        {
            5,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 6
        {
            6,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 7
        {
            7,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 8
        {
           8,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 9
        {
            9,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        },
    #endregion
    #region Region 10
        {
            10,
            new List<string>()
            {
                ""a"",
                ""b"",
                ""c"",
                ""d"",
                ""e"",
                ""f""
            }
        }$$
    
    #endregion
    };
#endif
}

class Program
{
    static void Main()
    {
    }
}

";
            #endregion

            AssertSmartIndent(
                code,
                expectedIndentation: 4);
        }
    }
}
