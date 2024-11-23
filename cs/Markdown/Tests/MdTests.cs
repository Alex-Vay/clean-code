using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using System.Text;

namespace Markdown.Tests.MdTests;

[TestFixture, NonParallelizable]
public class MdTests
{
    [TestCase("_окруженный с двух сторон_", "<em>окруженный с двух сторон</em>", TestName = "ShouldRender_ItalicWhenSurroundedByUnderscores")]
    [TestCase("__Выделенный двумя символами текст__", "<strong>Выделенный двумя символами текст</strong>", TestName = "ShouldRender_BoldWhenSurroundedByDoubleUnderscores")]
    [TestCase("\\_Вот это\\_", "_Вот это_", TestName = "ShouldIgnoreEscapedUnderscores")]
    [TestCase("Здесь сим\\волы экранирования\\ \\должны остаться.\\", "Здесь сим\\волы экранирования\\ \\должны остаться.\\", TestName = "ShouldPreserveEscapedCharacters")]
    [TestCase("\\\\_вот это будет выделено тегом_", "\\<em>вот это будет выделено тегом</em>", TestName = "ShouldEscapeBackslashesBeforeUnderscore")]
    [TestCase("__двойного выделения _одинарное_ тоже__", "<strong>двойного выделения <em>одинарное</em> тоже</strong>", TestName = "ShouldSupportNestedItalicInsideBold")]
    [TestCase("Но не наоборот — внутри _одинарного __двойное__ не_ работает", "Но не наоборот — внутри <em>одинарного __двойное__ не</em> работает", TestName = "ShouldNotAllowBoldInsideItalic")]
    [TestCase("цифрами_12_3", "цифрами_12_3", TestName = "ShouldNotItalicizeNumbersWithUnderscores")]
    [TestCase("_12_3", "<em>12</em>3", TestName = "ShouldItalicizeLeadingNumbers")]
    [TestCase("_нач_але, и в сер_еди_не, и в кон_це._", "<em>нач</em>але, и в сер<em>еди</em>не, и в кон<em>це.</em>", TestName = "ShouldItalicizePartsOfWords")]
    [TestCase("ра_зных сл_овах", "ра_зных сл_овах", TestName = "ShouldNotItalicizeAcrossWords")]
    [TestCase("__Непарные_ символы", "__Непарные_ символы", TestName = "ShouldNotRenderUnmatchedTags")]
    [TestCase("эти_ подчерки_ не считаются выделением", "эти_ подчерки_ не считаются выделением", TestName = "ShouldNotItalicizeUnderscores")]
    [TestCase("эти _подчерки _не считаются окончанием", "эти _подчерки _не считаются окончанием", TestName = "ShouldIgnoreUnmatchedClosingUnderscore")]
    [TestCase("__пересечения _двойных__ и одинарных_", "__пересечения _двойных__ и одинарных_", TestName = "ShouldHandleMixedUnmatchedTags")]
    [TestCase("Если внутри подчерков пустая строка ____", "Если внутри подчерков пустая строка ____", TestName = "ShouldNotRenderEmptyTags")]
    [TestCase("# Заголовок __с _разными_ символами__", "<h1>Заголовок <strong>с <em>разными</em> символами</strong></h1>", TestName = "ShouldRenderHeadersWithBoldAndItalic")]
    [TestCase("ра_зных _сл_овах", "ра_зных <em>сл</em>овах", TestName = "ShouldItalicizeOnlyValidInnerWords")]
    [TestCase("# Заголовки\n", "<h1>Заголовки</h1>\n", TestName = "ShouldRenderSimpleHeader")]
    [TestCase("[]()", "[]()", TestName = "ShouldIgnoreEmptyLinks")]
    [TestCase("[l](https://yandex.ru/ \"F\")", "<a href=\"https://yandex.ru/\" title=\"F\">l</a>", TestName = "ShouldRenderLinkWithTitle")]
    [TestCase("[l](https://yandex.ru/)", "<a href=\"https://yandex.ru/\">l</a>", TestName = "ShouldRenderLinkWithoutTitle")]
    [TestCase("\\[l](https://yandex.ru/)", "[l](https://yandex.ru/)", TestName = "ShouldEscapeSquareBracketsInLinks")]
    [TestCase("[l\\](https://yandex.ru/)", "[l](https://yandex.ru/)", TestName = "ShouldEscapeClosingBracketInsideLinkText")]
    [TestCase("[l]\\(https://yandex.ru/)", "[l]\\(https://yandex.ru/)", TestName = "ShouldEscapeParenthesesInLink")]
    [TestCase("# Заголовок _[l](https://yandex.ru/)_", "<h1>Заголовок <em><a href=\"https://yandex.ru/\">l</a></em></h1>", TestName = "ShouldRenderHeaderWithItalicizedLink")]
    [TestCase("____", "____", TestName = "ShouldIgnoreFourUnderscores")]
    public void Render_String_ShoulBeCorrect(string input, string expectedOutput) =>
        Md.Render(input).Should().Be(expectedOutput);

    [Test]
    [TestCase("_Курс_", 2, 10)]
    [TestCase("__Куhc__", 2, 14)]
    [TestCase("КУрс", 3, 10)]
    public void Render_Time_ShoulBeGrowLinearly(string textFragment, int powBasis, int numberPow)
    {
        var par = new List<TimeSpan>();
        var expected = powBasis / 2;
        for (int i = 0; i <= 10; i++)
        {
            var inputBuilder = new StringBuilder();
            for (var j = 0; j < Math.Pow(2, i); j++)
            {
                inputBuilder.Append(textFragment);
            }
            var input = inputBuilder.ToString();
            Md.Render("");
            var firstTimer = new Stopwatch();
            firstTimer.Start();
            Md.Render(input);
            firstTimer.Stop();
            par.Add(firstTimer.Elapsed);
        }

        var actual = 0;
        for (var i = 2; i < 10; i++)
        {
            if (par[i + 1] / par[i] < powBasis)
                actual += 1;
        }

        actual.Should().BeGreaterThan(expected);
    }
}
