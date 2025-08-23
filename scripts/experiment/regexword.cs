using System.Text.RegularExpressions;

//string pattern = @"\b$";
string pattern = @"\w$";
string[] inputs = [
    "a",
    "お",
    "(",
    "（",
    " ",
    "",
];
for (int i = 0; i < inputs.Length; i++) {
    bool match = Regex.IsMatch(inputs[i], pattern);
    Console.WriteLine($"Match{i} '{inputs[i]}': {match}");
}

//var rubyPattern = @"(^| )([^ \[]+)\[(.+?)\]";
//var rubyPattern = @"\b(\w+)\[(\w+)\]";
var rubyPattern = @"[ ]?(\w+)\[(\w+)\]";
var rubyReplace = @"<ruby>$1<rt>$2</rt></ruby>";
string[] rubyInputs = [
    "お 母[かあ]さんが",
    "（江坂[えさか]）",
    "(江坂[えさか])",
    "江坂[えさか]さん！",
];
for (int i = 0; i < rubyInputs.Length; i++) {
    var replaced = Regex.Replace(rubyInputs[i], rubyPattern, rubyReplace);
    Console.WriteLine($"Replace{i} '{rubyInputs[i]}': {replaced}");
    //Console.WriteLine($"1: {rubyInputs[i][^1]}");
}
