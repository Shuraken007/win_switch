using System;

namespace WinSwitchLayout // Note: actual namespace depends on the project name.
{
   internal class Program
   {
      static void Main(string[] args)
      {
         if (args.Length == 0)
         {
            Console.WriteLine("Invalid args");
            return;
         }

         var command = args[0];

         switch (command)
         {
            case "list" when args.Length == 1:
               var list = WinSwitchLayout.KeyboardLayoutHelper.GetList();
               Console.WriteLine(list);
               break;
            case "lang" when args.Length == 1:
               var res = WinSwitchLayout.KeyboardLayoutHelper.GetLang();
               Console.WriteLine(res);
               break;
            case "lang" when args.Length == 2:
               try
               {
                  var curLang = WinSwitchLayout.KeyboardLayoutHelper.SetLang(args[1]);
                  Console.WriteLine(curLang);
               }
               catch (InvalidLangException ex)
               {
                  Console.WriteLine(ex.GetType().FullName);
                  Console.WriteLine(ex.Message);
               }
               break;
            case "-l" when args.Length == 1:
               var xkb_list = WinSwitchLayout.KeyboardLayoutHelper.Xkb_Switch_List();
               Console.WriteLine(xkb_list);
               break;
            case "xkblang" when args.Length == 1:
               var xkbres = WinSwitchLayout.KeyboardLayoutHelper.Xkb_Switch_getXkbLayout();
               Console.WriteLine(xkbres);
               break;
            case "xkblang" when args.Length == 2:
               try
               {
                  WinSwitchLayout.KeyboardLayoutHelper.Xkb_Switch_setXkbLayout(args[1]);
               }
               catch (InvalidLangException ex)
               {
                  Console.WriteLine(ex.GetType().FullName);
                  Console.WriteLine(ex.Message);
               }
               break;
            default:
               Console.WriteLine("Invalid command");
               break;
         }

      }
   }
}