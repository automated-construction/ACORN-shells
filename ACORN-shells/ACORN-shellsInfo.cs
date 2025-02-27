﻿using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace ACORN
{
    public class ACORN_shellsInfo : GH_AssemblyInfo
  {
    public override string Name
    {
        get
        {
            return "ACORNshells";
        }
    }
    public override Bitmap Icon
    {
        get
        {
            //Return a 24x24 pixel bitmap to represent this GHA library.
            return null;
        }
    }
    public override string Description
    {
        get
        {
            //Return a short string describing the purpose of this GHA library.
            return "";
        }
    }
    public override Guid Id
    {
        get
        {
            return new Guid("e2d7910a-df19-4465-a45b-dd10c617b568");
        }
    }

    public override string AuthorName
    {
        get
        {
            //Return a string identifying you or your company.
            return "";
        }
    }
    public override string AuthorContact
    {
        get
        {
            //Return a string representing your preferred contact details.
            return "";
        }
    }
}
}
