import Header from "@/components/Header";
import Hero from "@/components/Hero";
import ForHosts from "@/components/ForHosts";
import ForDevelopers from "@/components/ForDevelopers";
import GettingStarted from "@/components/GettingStarted";
import DocsResources from "@/components/DocsResources";
import FAQ from "@/components/FAQ";
import Footer from "@/components/Footer";

const Index = () => (
  <div className="min-h-screen">
    <Header />
    <Hero />
    <ForHosts />
    <ForDevelopers />
    <GettingStarted />
    <DocsResources />
    <FAQ />
    <Footer />
  </div>
);

export default Index;
