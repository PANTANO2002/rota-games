using System.Diagnostics;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed class WarpConsentWindow : Form
{
    internal WarpConsentWindow()
    {
        Text = "Cloudflare WARP · preparar rota gratuita"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(650, 395); MinimumSize = MaximumSize = Size;
        BackColor = DashboardTheme.Canvas; ForeColor = DashboardTheme.Ink; Icon = BrandAssets.LoadIcon(); Font = FontAssets.Ui(11); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        var layout = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(24) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var height in new[]{45,115,50,55,60}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height)); Controls.Add(layout);
        layout.Controls.Add(new Label { Text="Uma alternativa gratuita pela Cloudflare",Dock=DockStyle.Fill,Font=FontAssets.Title(24),ForeColor=DashboardTheme.Ink },0,0);
        layout.Controls.Add(new Label { Text="O WARP é um componente da Cloudflare, separado do RotaGame. Quando escolhido, o tráfego de internet do PC passa pela rede dela.\n\nNão exige contratar WARP+. A melhora de ping será testada; ela não é garantida.",Dock=DockStyle.Fill,Font=FontAssets.Ui(11),ForeColor=DashboardTheme.Muted },0,1);
        var links=new DashboardFlowLayout {Dock=DockStyle.Fill,WrapContents=false}; AddLink("Termos da Cloudflare",WarpConsent.TermsUrl);AddLink("Privacidade",WarpConsent.PrivacyUrl);layout.Controls.Add(links,0,2);
        var agree=new CheckBox {Text="Li e aceito os termos e a política da Cloudflare para usar o WARP.",Checked=false,Dock=DockStyle.Fill,ForeColor=DashboardTheme.Ink,Font=FontAssets.Ui(10)};layout.Controls.Add(agree,0,3);
        var actions=new DashboardFlowLayout {Dock=DockStyle.Fill,WrapContents=false};
        var accept=new DashboardButton {Text="Aceitar e preparar",Enabled=false,Width=230,Height=43,BackColor=DashboardTheme.Accent,ForeColor=DashboardTheme.Ink};
        var cancel=new DashboardButton {Text="Agora não",Width=150,Height=43,BackColor=DashboardTheme.Raised,ForeColor=DashboardTheme.Ink};
        agree.CheckedChanged+=(_,_)=>accept.Enabled=agree.Checked; accept.Click+=(_,_)=> {if(agree.Checked){WarpConsent.AcceptByUser();DialogResult=DialogResult.OK;Close();}};cancel.Click+=(_,_)=>Close();
        actions.Controls.AddRange([accept,cancel]);layout.Controls.Add(actions,0,4);
        void AddLink(string text,string url){var link=new LinkLabel {Text=text,AutoSize=true,LinkColor=DashboardTheme.Green,Margin=new Padding(0,10,25,0)};link.LinkClicked+=(_,_)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});links.Controls.Add(link);}
    }
}
