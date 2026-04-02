import Header from "@/components/Header";
import Hero from "@/components/Hero";
import ForHosts from "@/components/ForHosts";
import ForDevelopers from "@/components/ForDevelopers";
import GettingStarted from "@/components/GettingStarted";
import Footer from "@/components/Footer";

const Index = () => (
  <div className="min-h-screen">
    <Header />
    <Hero />
    <ForHosts />
    <ForDevelopers />
    <GettingStarted />
    <Footer />
  </div>
);

export default Index;
