import { t } from "@lingui/core/macro";

export function localizeFieldLabel(label: string) {
  switch (label) {
    case "Hair type":
      return t`Hair type`;
    case "Colour notes":
      return t`Colour notes`;
    case "Allergies & sensitivities":
      return t`Allergies & sensitivities`;
    case "Preferred stylist":
      return t`Preferred stylist`;
    case "Birthday":
      return t`Birthday`;
    case "Usual cut":
      return t`Usual cut`;
    case "Clipper guard":
      return t`Clipper guard`;
    case "Beard style":
      return t`Beard style`;
    case "Preferred shape":
      return t`Preferred shape`;
    case "Gel or acrylic":
      return t`Gel or acrylic`;
    case "Colour preferences":
      return t`Colour preferences`;
    case "Nail condition notes":
      return t`Nail condition notes`;
    case "Goals":
      return t`Goals`;
    case "Injuries & limitations":
      return t`Injuries & limitations`;
    case "Fitness level":
      return t`Fitness level`;
    case "Emergency contact":
      return t`Emergency contact`;
    case "Emergency contact phone":
      return t`Emergency contact phone`;
    case "Student name":
      return t`Student name`;
    case "Grade":
      return t`Grade`;
    case "Subjects":
      return t`Subjects`;
    case "School":
      return t`School`;
    case "Curriculum":
      return t`Curriculum`;
    case "Parent / guardian":
      return t`Parent / guardian`;
    case "Guardian phone":
      return t`Guardian phone`;
    case "Learning notes":
      return t`Learning notes`;
    case "Pet name":
      return t`Pet name`;
    case "Species":
      return t`Species`;
    case "Breed":
      return t`Breed`;
    case "Pet date of birth":
      return t`Pet date of birth`;
    case "Sex / sterilised":
      return t`Sex / sterilised`;
    case "Weight (kg)":
      return t`Weight (kg)`;
    case "Microchip number":
      return t`Microchip number`;
    case "Vaccinations up to date":
      return t`Vaccinations up to date`;
    case "Last vaccination":
      return t`Last vaccination`;
    case "Medical conditions":
      return t`Medical conditions`;
    case "Medications":
      return t`Medications`;
    case "Temperament & handling":
      return t`Temperament & handling`;
    case "Pet insurance":
      return t`Pet insurance`;
    case "Date of birth":
      return t`Date of birth`;
    case "ID / passport number":
      return t`ID / passport number`;
    case "Medical aid scheme":
      return t`Medical aid scheme`;
    case "Medical aid number":
      return t`Medical aid number`;
    case "Allergies":
      return t`Allergies`;
    case "Chronic conditions":
      return t`Chronic conditions`;
    case "Current medications":
      return t`Current medications`;
    case "Referring practitioner":
      return t`Referring practitioner`;
    default:
      return label;
  }
}
