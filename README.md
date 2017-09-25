## The Teal;Deer

Interactive fiction author Andrew "zarf" Plotkin [suggested](http://eblong.com/zarf/essays/rule-based-if/index.html) a rule-based language for IF creation, where a rule is a body of imperative code that self-invoke when its attached condition becomes true.  It can have a tag attached, called an atom, which is a non-unique name.  His unresolved issue was how to specify in what order rules should fire in relation to each other.

Most of this language is straightforward (for being written in a weekend), but the only cool part is the ability to specify an ad-hoc group of rules, and state that they precede another group of rules.  The most interesting code in here is the code that checks this.  It sorts the groups into order, ensures a particular rule needn't precede itself, finds subset/superset/overlap/equality relations between groups like a Venn diagram, all with good error messages.

Whether or not it solved zarf's problem is (for me) beside the point.  It's a cool feature because it makes the program's *temporal* information explicit.  There aren't many languages with explicit temporal constructs, since the imperative & functional programming paradigms always list statements in chronological order.


## Inform 7 to Zarfian

Zarf's rule-based language for creating I-F: "zarfian".

It is relation-based, rule-based, and has a touch of aspect-oriented programming. 

An example of a "loves" relation in Inform7:

        Loving relates various people to various people.
        Alice loves Bob.
        Charlie loves Donna.
        
The same example in zarfian lacks the explicit declaration of the relation:

        do loves(Alice,Bob)
        do loves(Charlie, Donna)

(Most statements in zarfian start with the word "do", per Zarf's presentation at PenguiCon http://eblong.com/zarf/essays/rule-based-if/index.html which seeded this quick-n-dirty implementation.)

Inform7 can set relations like so.

        now Alice loves Edward.
        now Charlie does not love Donna.

The equivalent in Zarfian:

        loves(Alice, Edward) := true;
        loves(Charlie, Donna) := false;

Inform7 has several kinds of relations, such as various-to-various, one-to-various, "in groups", etc.  Zarfian has only the most general kind, various-to-various. However, Inform's relations are always binary: either Alice loves Bob or she doesn't.  Zarfian relations can be anything.

        loves(Alice,Edward) := 80;
        loves(Alice,Edward) := "Well, kinda.";
        loves(Alice,Edward) := { ...code that will do something later... };

(Note that although it can be anything, it can still only be one thing at a time. Those three statements ran in order would be pointless; only the final one's value would stick.)

Inform7 has computed relations.

        Joining relates a thing (called X) to a thing (called Y) when X is part of Y or Y is part of X.

Zarfian has it as well:

        do joined(thingX, thingY) if IsPart(thingX, thingY) || IsPart(thingY, thingX)

Inform has phrases which can do arbitrary things.

        To prevent (kid - a person) from sticking (inappropriate item - a thing) into (expensive item - a device):
                ...then do whatever...

The zarfian equivalent:

        do PreventStickingInto(kid,item,device) as { ...then do whatever... }

Inform has rules, such as:

        Carry out examining something:  ...do whatever...
        
Zarfian is all about rules:

        do CarryOutExamining(noun) as { ...do whatever... }


Inform7 groups rules into rulebooks:

        The cat behaviour rules are a rulebook.
        A cat behaviour rule: say "Meow."
        A cat behaviour rule when the laser pointer's dot is visible: "The cat pounces." 
        
Zarfian does as well, but like relations, does not explicitly declare the container's existance.

        do CatBehavior as { "Meow."; }
        do CatBehavior as { "The cat pounces."; } if IsVisible(LaserPointerDot)

(In the current implementation, an explicit "say" or "print" statement isn't needed.  Strings within braces, alone between semicolons, display themselves. Literal strings can use the \" to contain a double-quote, and \n to represent a newline, but all else is verbatim.)

Rulebooks can be invoked imperatively in Inform7.

        consider the cat behavior rules;

As well as Zarfian.
        
        CatBehavior;

Inform 7 has headings like Volume, Book, Chapter, and Section, which set off a group of constructs. Zarfian also has them and they work similarly: the initial word must immediately follow a newline, and the entirety of the line is considered the heading.  Unlike Inform7, the current implementation of Zarfian doesn't give numbering any special recognition.

Zarfian can declare a rule private by replacing the word "do", and can declare everything within a heading private. The current implementation defines Private as only one's peers can see it. (A private rule can only be seen by other rules in the same section. The contents of a private section can only be seen by the other sections within the same chapter. Etc. The contents of a private volume cannot be seen outside the file.)

        Section for implementation details - private
        
        do FiddleWithValue(value) as { ... } 
        
        private Count as 5

The current implementation doesn't actually enforce Private, but does understand and record it. It also doesn't support overriding Private with the "(see <heading>)" appellation. 

Inform7 has a description-of-objects, to select a subset of objects with which something will be done.

        ...suspicious persons which are in the Conservatory...

Zarfian does not have objects. But since object-based languages are based on the is-a and has-a relationships and zarfian is relation-based, zarfian can in theory support objects.

        do IsA(room, object)
        do IsA(person, thing)
        do IsA(thing, object)
        do HasA(thing, description)

Inform7 consults rulebooks in an order decided by imperative code.

        A specific action processing rule:
                follow the check rules for the action;
                follow the carry out rules for the action;
                follow the report rules for the action.

Zarfian can do so if needed.
        
        do InsertsInto(noun,noun2) as {
                CheckInsertsInto(noun,noun2);
                CarryOutInsertsInto(noun,noun2);
                ReportInsertsInto(noun,noun2);
        }

But Zarfian prefers a description-of-rules, to select a subset of rules.  Currently the only use of such is the "precede" relation for declaring the relative order in which groups of rules run. The current implementation uses the special syntax "<description of rules> precede <description of rules>" instead of the usual "do precede(<description>,<description>)". This is to indicate the uniqueness of precedence rules as instructions to the compiler. Unlike all other rules, precedence rules do not exist at run-time. 

        rules named Check* precede rules named CarryOut*
        rules named CarryOut* precede rules named Report*
        rules in Volume 1 - cloak of darkness precede rules in Volume - the Standard Rules
        rules checking Verb precede rules checking TurnCount
        rules changing Score precede rules reading Score

The clauses in a rule description are "named", "in", "checking", "changing", and "reading".  "Named" performs a wildcard match on the rulebook's name. (Individual rules do not have names). The \* asterisk can be used anywhere in the pattern, and multiple times, such as \*Putting\* to catch CheckPuttingOn and ReportPuttingDown.  "In" refers to everything within a heading.  "Checking" refers to a variable mentioned in a rule's "if" clause. "Reading" refers to a variable mentioned in a rule's "as" clause. "Changing" refers to a variable that appears on the left-hand side of the := assignment operator within a rule's "as" clause.  

In the current implementation, these clauses can be combined, though the same clause can't be used twice in the same description. I think this is good enough for a prototype though a real implementation would likely want to add that as well.  Additionally it might allow a "description-of-expressions" so we could identify rules which do "any math operation" on "any variable named attack\*" and other arbitrary selections. But the current implementation gives the flavor of the feature with half the work.

After the compiler gathers all the rules from all the source files, extensions, and the standard library, it then examines the precedence rules.  The current implementation first ensures no rule will appear on both side of a precedence rule.  For example, there's probably a rule somewhere that both reads and changes the score, so the above precedence rule is saying that particular rule would have to precede itself.  Secondly, the current implementation performs a simple check for cyclical reasoning, such as if the author had also stated "rules named Report\* precede rules named Check\*" in addition to the above two. 

In this way groups of rules are sorted into a chronological partial order by way of compiler directive. 
